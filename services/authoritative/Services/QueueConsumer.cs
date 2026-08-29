using System.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Authoritative.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Authoritative.Domain;
using System.Collections.Concurrent;

namespace Authoritative.Services;

public class QueueConsumer : BackgroundService
{
    internal const string ActionsQueueName = "actions";
    internal const string DeadLetterQueueName = "actions.dead-letter";
    static readonly TimeSpan RabbitMqRetryDelay = TimeSpan.FromSeconds(5);
    static readonly TimeSpan DeadLetterConfirmTimeout = TimeSpan.FromSeconds(5);

    readonly ILogger<QueueConsumer> _log;
    readonly Authoritative.Domain.IItemGenerator _generator;
    readonly IGeneratedItemStore _itemStore;
    readonly IDiagnosticLogStore _diagnosticLogs;
    readonly IAuthoritativeMetrics _metrics;
    IConnection? _connection;
    IModel? _channel;
    QueueDeliveryProcessor? _processor;
    IMessageSettlement? _settlement;
    readonly IProcessedMessageStore _processedMessages = new InMemoryProcessedMessageStore();

    public QueueConsumer(
        ILogger<QueueConsumer> log,
        Authoritative.Domain.IItemGenerator generator,
        IGeneratedItemStore itemStore,
        IDiagnosticLogStore diagnosticLogs,
        IAuthoritativeMetrics metrics)
    {
        _log = log;
        _generator = generator;
        _itemStore = itemStore;
        _diagnosticLogs = diagnosticLogs;
        _metrics = metrics;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return StartWithRabbitMqRetryAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null || _processor == null || _settlement == null)
            return;

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var delivery = new QueueDelivery(ea.Body.ToArray(), ea.RoutingKey, ea.DeliveryTag);
            _processor.Process(delivery, _settlement);
        };

        _channel.BasicConsume(queue: ActionsQueueName, autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    async Task StartWithRabbitMqRetryAsync(CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ConfirmSelect();
                _channel.QueueDeclare(queue: ActionsQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.QueueDeclare(queue: DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.BasicQos(0, 1, false);

                var acknowledger = new RabbitMqMessageAcknowledger(_channel);
                var deadLetterPublisher = new RabbitMqDeadLetterPublisher(_channel, DeadLetterQueueName, DeadLetterConfirmTimeout);
                _settlement = new QueueMessageSettlement(deadLetterPublisher, acknowledger, _log, _diagnosticLogs, _metrics);
                _processor = new QueueDeliveryProcessor(_generator, _itemStore, _processedMessages, _log, _diagnosticLogs, _metrics);

                _log.LogInformation("Connected to RabbitMQ at {host}:{port}", factory.HostName, factory.Port);
                RecordDiagnostic("Information", "queue.rabbitmq", "rabbitmq.connected", "Connected to RabbitMQ.",
                    properties: new Dictionary<string, string>
                    {
                        ["host"] = factory.HostName,
                        ["port"] = factory.Port.ToString()
                    });
                await base.StartAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                CloseRabbitMqResources();
                _log.LogWarning(ex, "RabbitMQ is unavailable; retrying in {delaySeconds} seconds.", RabbitMqRetryDelay.TotalSeconds);
                RecordDiagnostic("Warning", "queue.rabbitmq", "rabbitmq.connection_retry", "RabbitMQ is unavailable; retrying.",
                    ex,
                    properties: new Dictionary<string, string>
                    {
                        ["retryDelaySeconds"] = RabbitMqRetryDelay.TotalSeconds.ToString()
                    });
                await Task.Delay(RabbitMqRetryDelay, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    static ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName = GetEnvironmentValue("RABBITMQ_HOST", "rabbitmq"),
            Port = GetEnvironmentInt("RABBITMQ_PORT", 5672),
            UserName = GetEnvironmentValue("RABBITMQ_USERNAME", "guest"),
            Password = GetEnvironmentValue("RABBITMQ_PASSWORD", "guest"),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    static string GetEnvironmentValue(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    static int GetEnvironmentInt(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        CloseRabbitMqResources();
        return base.StopAsync(cancellationToken);
    }

    void CloseRabbitMqResources()
    {
        try
        {
            _channel?.Close();
        }
        catch
        {
            // Shutdown is best-effort; the process is already stopping or retrying.
        }
        finally
        {
            _channel?.Dispose();
            _channel = null;
        }

        try
        {
            _connection?.Close();
        }
        catch
        {
            // Shutdown is best-effort; the process is already stopping or retrying.
        }
        finally
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    void RecordDiagnostic(
        string level,
        string category,
        string eventName,
        string message,
        Exception? exception = null,
        Dictionary<string, string>? properties = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string sourceMember = "",
        [CallerLineNumber] int sourceLine = 0)
    {
        try
        {
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = level,
                Category = category,
                EventName = eventName,
                Message = message,
                Properties = properties
            }, exception, sourceFile, sourceMember, sourceLine);
        }
        catch (Exception diagnosticException)
        {
            _log.LogWarning(diagnosticException, "Failed to write diagnostic log entry.");
        }
    }
}

internal sealed class QueueDelivery
{
    public QueueDelivery(byte[] body, string routingKey, ulong deliveryTag)
    {
        Body = body;
        RoutingKey = routingKey;
        DeliveryTag = deliveryTag;
    }

    public byte[] Body { get; }
    public string RoutingKey { get; }
    public ulong DeliveryTag { get; }

    public string BodyAsUtf8()
    {
        return Encoding.UTF8.GetString(Body);
    }
}

internal interface IMessageSettlement
{
    void Ack(ulong deliveryTag);
    void DeadLetterAndAck(QueueDelivery delivery, string json, Exception exception);
}

internal interface IMessageAcknowledger
{
    void Ack(ulong deliveryTag);
    void Nack(ulong deliveryTag, bool requeue);
}

internal interface IDeadLetterPublisher
{
    void Publish(QueueDelivery delivery, Exception exception);
}

internal sealed class QueueDeliveryProcessor
{
    readonly Authoritative.Domain.IItemGenerator _generator;
    readonly IGeneratedItemStore _itemStore;
    readonly IProcessedMessageStore _processedMessages;
    readonly ILogger _log;
    readonly IDiagnosticLogStore _diagnosticLogs;
    readonly IAuthoritativeMetrics _metrics;

    public QueueDeliveryProcessor(
        Authoritative.Domain.IItemGenerator generator,
        IGeneratedItemStore itemStore,
        IProcessedMessageStore processedMessages,
        ILogger log,
        IDiagnosticLogStore? diagnosticLogs = null,
        IAuthoritativeMetrics? metrics = null)
    {
        _generator = generator;
        _itemStore = itemStore;
        _processedMessages = processedMessages;
        _log = log;
        _diagnosticLogs = diagnosticLogs ?? NullDiagnosticLogStore.Instance;
        _metrics = metrics ?? NullAuthoritativeMetrics.Instance;
    }

    public void Process(QueueDelivery delivery, IMessageSettlement settlement)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var json = "";
        var shouldAck = false;
        string? startedMessageId = null;
        var completedMutation = false;
        var actionType = "unknown";
        try
        {
            json = delivery.BodyAsUtf8();
            var envelope = ActionEnvelope.ParseAndValidate(json, DateTime.UtcNow);
            actionType = envelope.Type;
            _metrics.RecordCommandReceived(actionType);
            _log.LogInformation("Dequeued action: {action}", json);
            RecordDiagnostic("Information", "queue.actions", "action.dequeued", "Dequeued authoritative action.",
                payload: json,
                properties: new Dictionary<string, string>
                {
                    ["routingKey"] = delivery.RoutingKey,
                    ["deliveryTag"] = delivery.DeliveryTag.ToString(),
                    ["bytes"] = delivery.Body.Length.ToString(),
                    ["messageId"] = envelope.MessageId,
                    ["contractVersion"] = envelope.ContractVersion.ToString(),
                    ["actionType"] = envelope.Type
                });

            if (!_processedMessages.TryStart(envelope.MessageId))
            {
                _log.LogInformation("Skipping duplicate authoritative action {messageId}", envelope.MessageId);
                RecordDiagnostic("Information", "queue.actions", "action.duplicate", "Skipped duplicate authoritative action.",
                    payload: json,
                    properties: new Dictionary<string, string>
                    {
                        ["routingKey"] = delivery.RoutingKey,
                        ["deliveryTag"] = delivery.DeliveryTag.ToString(),
                        ["messageId"] = envelope.MessageId,
                        ["actionType"] = envelope.Type
                });
                _metrics.RecordCommandDuplicate(actionType);
                shouldAck = true;
            }
            else
            {
                startedMessageId = envelope.MessageId;
                var item = _generator.GenerateUniqueItem();
                var metadata = envelope.Payload.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                metadata["messageId"] = envelope.MessageId;
                metadata["contractVersion"] = envelope.ContractVersion.ToString();
                metadata["actionType"] = envelope.Type;
                _itemStore.SaveGeneratedItem(item, metadata);
                _processedMessages.MarkCompleted(envelope.MessageId);
                completedMutation = true;
                shouldAck = true;
                _metrics.RecordCommandSucceeded(actionType);
                _log.LogInformation("Generated item {id} type={type}", item.Id, item.Type);
                RecordDiagnostic("Information", "gameplay.items", "item.generated", "Generated authoritative item.",
                    entityId: item.Id,
                    properties: new Dictionary<string, string>
                    {
                        ["itemId"] = item.Id,
                        ["itemType"] = item.Type,
                        ["itemTier"] = item.Tier,
                        ["messageId"] = envelope.MessageId,
                        ["actionType"] = envelope.Type
                    });
            }
        }
        catch (Exception ex)
        {
            if (startedMessageId != null && !completedMutation)
                _processedMessages.MarkFailed(startedMessageId);

            if (ActionEnvelope.IsValidationFailure(ex))
                _metrics.RecordCommandValidationFailure(ActionEnvelope.ValidationFailureReason(ex));
            else
                _metrics.RecordCommandProcessingFailure(actionType);

            RecordDiagnostic("Error", "queue.actions", "action.processing_failed", "Failed to process authoritative action.",
                ex,
                payload: json,
                properties: new Dictionary<string, string>
                {
                    ["routingKey"] = delivery.RoutingKey,
                    ["deliveryTag"] = delivery.DeliveryTag.ToString()
                });
            settlement.DeadLetterAndAck(delivery, json, ex);
            return;
        }

        if (!shouldAck)
            return;

        settlement.Ack(delivery.DeliveryTag);
        _metrics.RecordAckLatency(Stopwatch.GetElapsedTime(startedAt));
        RecordDiagnostic("Information", "queue.actions", "action.acked", "Acknowledged authoritative action.",
            properties: new Dictionary<string, string>
            {
                ["routingKey"] = delivery.RoutingKey,
                ["deliveryTag"] = delivery.DeliveryTag.ToString()
            });
    }

    void RecordDiagnostic(
        string level,
        string category,
        string eventName,
        string message,
        Exception? exception = null,
        string? payload = null,
        string? entityId = null,
        Dictionary<string, string>? properties = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string sourceMember = "",
        [CallerLineNumber] int sourceLine = 0)
    {
        try
        {
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = level,
                Category = category,
                EventName = eventName,
                Message = message,
                EntityId = entityId,
                Payload = payload,
                Properties = properties
            }, exception, sourceFile, sourceMember, sourceLine);
        }
        catch (Exception diagnosticException)
        {
            _log.LogWarning(diagnosticException, "Failed to write diagnostic log entry.");
        }
    }
}

internal sealed class ActionEnvelope
{
    public const int CurrentContractVersion = 1;
    const int MaxMessageIdLength = 128;
    static readonly HashSet<string> SupportedActionTypes = new(StringComparer.Ordinal)
    {
        "spawn_item"
    };

    public int ContractVersion { get; init; }
    public string MessageId { get; init; } = "";
    public string Type { get; init; } = "";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public JObject Payload { get; init; } = new();

    public static bool IsValidationFailure(Exception exception)
    {
        return exception is InvalidOperationException invalidOperation
            && (invalidOperation.Message.StartsWith("Action envelope ", StringComparison.Ordinal)
                || invalidOperation.Message.StartsWith("Invalid action envelope:", StringComparison.Ordinal));
    }

    public static string ValidationFailureReason(Exception exception)
    {
        if (exception.Message.StartsWith("Invalid action envelope:", StringComparison.Ordinal))
            return "schema";

        if (exception.Message.Contains("not valid JSON", StringComparison.Ordinal))
            return "invalid_json";

        if (exception.Message.Contains("could not be deserialized", StringComparison.Ordinal))
            return "deserialize";

        return "invalid";
    }

    public static ActionEnvelope ParseAndValidate(string json, DateTime nowUtc)
    {
        ActionEnvelope? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<ActionEnvelope>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Action envelope is not valid JSON.", ex);
        }

        if (envelope == null)
            throw new InvalidOperationException("Action envelope could not be deserialized.");

        var errors = envelope.Validate(nowUtc).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException($"Invalid action envelope: {string.Join("; ", errors)}");

        return envelope;
    }

    IEnumerable<string> Validate(DateTime nowUtc)
    {
        if (ContractVersion != CurrentContractVersion)
            yield return $"unsupported contractVersion '{ContractVersion}'";

        if (string.IsNullOrWhiteSpace(MessageId))
            yield return "messageId is required";
        else if (MessageId.Length > MaxMessageIdLength)
            yield return $"messageId must be {MaxMessageIdLength} characters or fewer";

        if (string.IsNullOrWhiteSpace(Type))
            yield return "type is required";
        else if (!SupportedActionTypes.Contains(Type))
            yield return $"unsupported action type '{Type}'";

        if (CreatedAtUtc == default)
            yield return "createdAtUtc is required";
        else if (CreatedAtUtc.Kind == DateTimeKind.Local)
            yield return "createdAtUtc must be UTC";

        if (ExpiresAtUtc.HasValue)
        {
            if (ExpiresAtUtc.Value.Kind == DateTimeKind.Local)
                yield return "expiresAtUtc must be UTC";
            else if (ExpiresAtUtc.Value <= nowUtc)
                yield return "message is stale";
        }

        if (Payload == null)
            yield return "payload is required";
    }
}

internal interface IProcessedMessageStore
{
    bool TryStart(string messageId);
    void MarkCompleted(string messageId);
    void MarkFailed(string messageId);
}

internal sealed class InMemoryProcessedMessageStore : IProcessedMessageStore
{
    readonly ConcurrentDictionary<string, bool> _messageIds = new(StringComparer.Ordinal);

    public bool TryStart(string messageId)
    {
        return _messageIds.TryAdd(messageId, false);
    }

    public void MarkCompleted(string messageId)
    {
        _messageIds[messageId] = true;
    }

    public void MarkFailed(string messageId)
    {
        _messageIds.TryRemove(messageId, out _);
    }
}

internal sealed class QueueMessageSettlement : IMessageSettlement
{
    readonly IDeadLetterPublisher _deadLetterPublisher;
    readonly IMessageAcknowledger _acknowledger;
    readonly ILogger _log;
    readonly IDiagnosticLogStore _diagnosticLogs;
    readonly IAuthoritativeMetrics _metrics;

    public QueueMessageSettlement(
        IDeadLetterPublisher deadLetterPublisher,
        IMessageAcknowledger acknowledger,
        ILogger log,
        IDiagnosticLogStore? diagnosticLogs = null,
        IAuthoritativeMetrics? metrics = null)
    {
        _deadLetterPublisher = deadLetterPublisher;
        _acknowledger = acknowledger;
        _log = log;
        _diagnosticLogs = diagnosticLogs ?? NullDiagnosticLogStore.Instance;
        _metrics = metrics ?? NullAuthoritativeMetrics.Instance;
    }

    public void Ack(ulong deliveryTag)
    {
        _acknowledger.Ack(deliveryTag);
    }

    public void DeadLetterAndAck(QueueDelivery delivery, string json, Exception exception)
    {
        try
        {
            _deadLetterPublisher.Publish(delivery, exception);
            _acknowledger.Ack(delivery.DeliveryTag);
            _metrics.RecordDeadLetterPublished();
            _log.LogError(exception, "Moved failed action to {queue}: {action}", QueueConsumer.DeadLetterQueueName, json);
            RecordDiagnostic("Error", "queue.actions", "action.dead_lettered", "Moved failed authoritative action to the dead-letter queue.",
                exception,
                payload: json,
                properties: new Dictionary<string, string>
                {
                    ["queue"] = QueueConsumer.DeadLetterQueueName,
                    ["routingKey"] = delivery.RoutingKey,
                    ["deliveryTag"] = delivery.DeliveryTag.ToString()
                });
        }
        catch (Exception deadLetterException)
        {
            _log.LogError(deadLetterException, "Failed to dead-letter action. Requeueing original message.");
            _metrics.RecordDeadLetterFailed();
            RecordDiagnostic("Critical", "queue.actions", "action.dead_letter_failed", "Failed to dead-letter authoritative action; requeueing original message.",
                deadLetterException,
                payload: json,
                properties: new Dictionary<string, string>
                {
                    ["queue"] = QueueConsumer.DeadLetterQueueName,
                    ["routingKey"] = delivery.RoutingKey,
                    ["deliveryTag"] = delivery.DeliveryTag.ToString()
                });
            _acknowledger.Nack(delivery.DeliveryTag, requeue: true);
        }
    }

    void RecordDiagnostic(
        string level,
        string category,
        string eventName,
        string message,
        Exception? exception = null,
        string? payload = null,
        Dictionary<string, string>? properties = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string sourceMember = "",
        [CallerLineNumber] int sourceLine = 0)
    {
        try
        {
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = level,
                Category = category,
                EventName = eventName,
                Message = message,
                Payload = payload,
                Properties = properties
            }, exception, sourceFile, sourceMember, sourceLine);
        }
        catch (Exception diagnosticException)
        {
            _log.LogWarning(diagnosticException, "Failed to write diagnostic log entry.");
        }
    }
}

internal sealed class RabbitMqMessageAcknowledger : IMessageAcknowledger
{
    readonly IModel _channel;

    public RabbitMqMessageAcknowledger(IModel channel)
    {
        _channel = channel;
    }

    public void Ack(ulong deliveryTag)
    {
        _channel.BasicAck(deliveryTag, multiple: false);
    }

    public void Nack(ulong deliveryTag, bool requeue)
    {
        _channel.BasicNack(deliveryTag, multiple: false, requeue: requeue);
    }
}

internal sealed class RabbitMqDeadLetterPublisher : IDeadLetterPublisher
{
    readonly IModel _channel;
    readonly string _deadLetterQueueName;
    readonly TimeSpan _confirmTimeout;

    public RabbitMqDeadLetterPublisher(IModel channel, string deadLetterQueueName, TimeSpan confirmTimeout)
    {
        _channel = channel;
        _deadLetterQueueName = deadLetterQueueName;
        _confirmTimeout = confirmTimeout;
    }

    public void Publish(QueueDelivery delivery, Exception exception)
    {
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-error-message"] = exception.Message,
            ["x-original-routing-key"] = delivery.RoutingKey,
            ["x-failed-at-utc"] = DateTime.UtcNow.ToString("O")
        };

        _channel.BasicPublish(
            exchange: "",
            routingKey: _deadLetterQueueName,
            basicProperties: properties,
            body: delivery.Body);
        _channel.WaitForConfirmsOrDie(_confirmTimeout);
    }
}
