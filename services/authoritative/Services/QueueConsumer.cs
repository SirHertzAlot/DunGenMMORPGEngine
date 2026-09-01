#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Domain;
#if !UNITY_5_3_OR_NEWER
using Newtonsoft.Json;
#endif

#if !UNITY_5_3_OR_NEWER
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
#endif

namespace Authoritative.Services
{
#if UNITY_5_3_OR_NEWER
    public sealed class QueueConsumer
#else
    public class QueueConsumer : BackgroundService
#endif
    {
#if UNITY_5_3_OR_NEWER
        readonly IAuthoritativeLogger _log;
        readonly ConcurrentQueue<string> _pendingMessages = new();
        readonly SemaphoreSlim _pendingSignal = new(0);
        CancellationTokenSource? _workerCancellation;
        Task? _workerTask;
#else
        static readonly Counter ReceivedActions = Metrics.CreateCounter(
            "authoritative_actions_received_total",
            "Total action messages received from queue.");
        static readonly Counter GeneratedItems = Metrics.CreateCounter(
            "authoritative_generated_items_total",
            "Total generated items emitted by spawn_item actions.");
        static readonly Counter IgnoredActions = Metrics.CreateCounter(
            "authoritative_actions_ignored_total",
            "Total action messages ignored due to type mismatch or invalid payload.");
        static readonly Counter FailedActions = Metrics.CreateCounter(
            "authoritative_actions_failed_total",
            "Total queue actions that failed processing.");
        static readonly Histogram ActionProcessingSeconds = Metrics.CreateHistogram(
            "authoritative_action_processing_seconds",
            "Latency of queue action processing in seconds.");

        readonly ILogger<QueueConsumer> _log;
        IConnection? _connection;
        IModel? _channel;
#endif

        readonly Authoritative.Domain.IItemGenerator _generator;
        readonly IGeneratedItemStore _itemStore;
    #if !UNITY_5_3_OR_NEWER
        readonly IAdminObservabilityService? _observability;
        readonly IWorldStreamEmitter? _stream;
    #endif

#if UNITY_5_3_OR_NEWER
        public QueueConsumer(
            Authoritative.Domain.IItemGenerator generator,
            IGeneratedItemStore itemStore,
            IAuthoritativeLogger? log = null)
        {
            _generator = generator;
            _itemStore = itemStore;
            _log = log ?? new UnityAuthoritativeLogger();
        }
#else
        public QueueConsumer(
            ILogger<QueueConsumer> log,
            Authoritative.Domain.IItemGenerator generator,
            IGeneratedItemStore itemStore)
            : this(log, generator, itemStore, null, null)
        {
        }

        public QueueConsumer(
            ILogger<QueueConsumer> log,
            Authoritative.Domain.IItemGenerator generator,
            IGeneratedItemStore itemStore,
            IAdminObservabilityService? observability)
            : this(log, generator, itemStore, observability, null)
        {
        }

        public QueueConsumer(
            ILogger<QueueConsumer> log,
            Authoritative.Domain.IItemGenerator generator,
            IGeneratedItemStore itemStore,
            IAdminObservabilityService? observability,
            IWorldStreamEmitter? stream)
        {
            _log = log;
            _generator = generator;
            _itemStore = itemStore;
            _observability = observability;
            _stream = stream;
        }
#endif

#if UNITY_5_3_OR_NEWER
        public void EnqueueAction(ActionMessage action)
        {
            var json = SerializeAction(action);
            _pendingMessages.Enqueue(json);
            _pendingSignal.Release();
        }

        public void EnqueueRawActionJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            _pendingMessages.Enqueue(json);
            _pendingSignal.Release();
        }

        public int ProcessBatch(int maxMessages)
        {
            if (maxMessages <= 0)
                return 0;

            int processed = 0;
            while (processed < maxMessages && _pendingMessages.TryDequeue(out var json))
            {
                ProcessActionJson(json);
                processed++;
            }

            return processed;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_workerTask != null && !_workerTask.IsCompleted)
                return _workerTask;

            _workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _workerTask = RunWorkerLoopAsync(_workerCancellation.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_workerCancellation == null || _workerTask == null)
                return;

            _workerCancellation.Cancel();
            _pendingSignal.Release();

            var finished = await Task.WhenAny(_workerTask, Task.Delay(Timeout.Infinite, cancellationToken));
            if (finished != _workerTask)
                throw new OperationCanceledException(cancellationToken);

            _workerCancellation.Dispose();
            _workerCancellation = null;
            _workerTask = null;
        }

        async Task RunWorkerLoopAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation("QueueConsumer worker loop started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _pendingSignal.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (_pendingMessages.TryDequeue(out var json))
                {
                    ProcessActionJson(json);
                }
            }

            _log.LogInformation("QueueConsumer worker loop stopped.");
        }
#else
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory() { HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "actions", durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQos(0, 1, false); // process one at a time (FIFO behavior per queue)
            _log.LogInformation("Connected to RabbitMQ at {host}", factory.HostName);
            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return Task.CompletedTask;

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                ReceivedActions.Inc();
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    using (ActionProcessingSeconds.NewTimer())
                    {
                        var outcome = ProcessActionJson(json);
                        if (outcome == ActionProcessOutcome.GeneratedItem)
                            GeneratedItems.Inc();
                        else
                            IgnoredActions.Inc();
                    }

                    // ACK after processing
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    FailedActions.Inc();
                    _log.LogError(ex, "Error processing message");
                    // do not ack so message can be retried / dead-lettered
                }
            };

            _channel.BasicConsume(queue: "actions", autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _connection?.Close();
            return base.StopAsync(cancellationToken);
        }
#endif

        ActionProcessOutcome ProcessActionJson(string json)
        {
            var action = DeserializeAction(json);
            _log.LogInformation("Dequeued action: {action}", json);

            if (action == null)
            {
                RecordAction("unknown", "invalid");
                return ActionProcessOutcome.Invalid;
            }

            EmitWorldEventFromAction(action, "received");

            if (!string.Equals(action.Type, "spawn_item", StringComparison.Ordinal))
            {
                RecordAction(action.Type, "ignored", action.Payload);
                EmitWorldEventFromAction(action, "ignored");
                return ActionProcessOutcome.Ignored;
            }

            var item = _generator.GenerateUniqueItem();
            _itemStore.SaveGeneratedItem(item, action.Payload);
            _log.LogInformation("Generated item {id} type={type}", item.Id, item.Type);
            RecordAction(action.Type, "generated_item", action.Payload);
            EmitWorldEventFromAction(action, "completed");
            return ActionProcessOutcome.GeneratedItem;
        }

        void RecordAction(string actionType, string outcome, IReadOnlyDictionary<string, string>? metadata = null)
        {
#if !UNITY_5_3_OR_NEWER
            _observability?.RecordAction(actionType, outcome, metadata);
#endif
        }

        void EmitWorldEventFromAction(ActionMessage action, string stage)
        {
            var payload = action.Payload;
            if (payload == null || !payload.TryGetValue("sessionId", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
                return;

            uint frame = 0;
            if (payload.TryGetValue("frame", out var frameRaw))
            {
                uint.TryParse(frameRaw, out frame);
            }

            payload.TryGetValue("entityId", out var entityId);

#if !UNITY_5_3_OR_NEWER
            _stream?.EmitAsync(new WorldStreamMessage
            {
                Type = $"action.{stage}",
                SessionId = sessionId,
                Frame = frame,
                EntityId = entityId ?? string.Empty,
                Data = new Dictionary<string, string>(payload, StringComparer.Ordinal)
                {
                    ["actionType"] = action.Type,
                    ["stage"] = stage
                }
            });

            if (_observability == null)
                return;

            payload.TryGetValue("message", out var message);

            _observability.RecordWorldEvent(new WorldSessionEvent
            {
                SessionId = sessionId,
                EventType = action.Type,
                Category = payload.TryGetValue("category", out var category) ? category : "queue-action",
                Frame = frame,
                EntityId = entityId ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(message)
                    ? $"Action '{action.Type}' {stage}."
                    : message,
                TimestampUtc = DateTime.UtcNow,
                Data = new Dictionary<string, string>(payload, StringComparer.Ordinal)
                {
                    ["stage"] = stage
                }
            });
#endif
        }

        enum ActionProcessOutcome
        {
            Invalid,
            Ignored,
            GeneratedItem
        }

        static string SerializeAction(ActionMessage action)
        {
#if !UNITY_5_3_OR_NEWER
            return JsonConvert.SerializeObject(action);
#else
            var payload = action.Payload == null || action.Payload.Count == 0
                ? string.Empty
                : string.Join(";", action.Payload.Select(kv => Encode(kv.Key) + "=" + Encode(kv.Value)));
            return Encode(action.Type) + "|" + payload;
#endif
        }

        static ActionMessage? DeserializeAction(string raw)
        {
#if !UNITY_5_3_OR_NEWER
            return JsonConvert.DeserializeObject<ActionMessage>(raw);
#else
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var parts = raw.Split(new[] { '|' }, 2);
            var type = parts.Length > 0 ? Decode(parts[0]) : string.Empty;
            var payload = new Dictionary<string, string>(StringComparer.Ordinal);

            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
            {
                var entries = parts[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    var kv = entry.Split(new[] { '=' }, 2);
                    if (kv.Length != 2)
                        continue;

                    payload[Decode(kv[0])] = Decode(kv[1]);
                }
            }

            return new ActionMessage
            {
                Type = type,
                Payload = payload.Count == 0 ? null : payload
            };
#endif
        }

#if UNITY_5_3_OR_NEWER
        static string Encode(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
#endif
    }

#if UNITY_5_3_OR_NEWER
    public interface IAuthoritativeLogger
    {
        void LogInformation(string message, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
    }

    public sealed class UnityAuthoritativeLogger : IAuthoritativeLogger
    {
        public void LogInformation(string message, params object[] args)
        {
            UnityEngine.Debug.Log(Format(message, args));
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            UnityEngine.Debug.LogError($"{Format(message, args)} | Exception: {exception}");
        }

        static string Format(string message, object[] args)
        {
            if (args == null || args.Length == 0)
                return message;

            return message + " " + string.Join(", ", args);
        }
    }
#endif
}
