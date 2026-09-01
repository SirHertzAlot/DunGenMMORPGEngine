#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace Authoritative.Services
{
    /// <summary>
    /// Fans near-real-time world events out to connected clients (Three.js
    /// visualizer and game clients). A single background consumer reads the
    /// RabbitMQ "world.events" queue and pushes each message to every connected
    /// WebSocket whose session filter matches. On attach, a client first catches
    /// up from the Redis hot-cache buffer so recent frames are never missed.
    /// ScyllaDB/Postgres remain the durable stores; this relay is the live pipe.
    /// </summary>
    public interface IWorldStreamRelay
    {
        Task CatchUpAsync(WebSocket socket, string sessionId, CancellationToken ct);
        void Subscribe(WebSocket socket, string? sessionId);
        void Unsubscribe(WebSocket socket);
    }

    public sealed class WorldStreamRelay : BackgroundService, IWorldStreamRelay
    {
        const string QueueName = "world.events";
        const int MaxCatchUpEvents = 500;

        static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };

        readonly ILogger<WorldStreamRelay> _log;
        readonly bool _enabled;
        readonly ConcurrentDictionary<WebSocket, string?> _clients = new();
        readonly IDatabase? _redis;
        IConnection? _connection;
        IModel? _channel;

        public WorldStreamRelay(IConfiguration config, ILogger<WorldStreamRelay> log)
        {
            _log = log;
            _enabled = string.Equals(config["WORLD_STREAM_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);

            if (!_enabled)
                return;

            var redisHost = string.IsNullOrWhiteSpace(config["REDIS_HOST"])
                ? Environment.GetEnvironmentVariable("REDIS_HOST")
                : config["REDIS_HOST"];
            if (string.IsNullOrWhiteSpace(redisHost))
                redisHost = "redis";
            try
            {
                _redis = ConnectionMultiplexer.Connect($"{redisHost}:6379,abortConnect=false,connectTimeout=5000").GetDatabase();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "World stream relay Redis unavailable; live fan-out still works, catch-up disabled.");
            }
        }

        public async Task CatchUpAsync(WebSocket socket, string sessionId, CancellationToken ct)
        {
            if (_redis == null || socket.State != WebSocketState.Open)
                return;

            var listKey = $"ws:buffer:{sessionId}";
            var buffered = await _redis.ListRangeAsync(listKey, -MaxCatchUpEvents, -1).ConfigureAwait(false);
            foreach (var entry in buffered)
            {
                if (socket.State != WebSocketState.Open)
                    break;
                await SendRawAsync(socket, entry!, ct).ConfigureAwait(false);
            }
        }

        public void Subscribe(WebSocket socket, string? sessionId)
            => _clients[socket] = sessionId;

        public void Unsubscribe(WebSocket socket)
            => _clients.TryRemove(socket, out _);

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
                return Task.CompletedTask;

            try
            {
                var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
                var factory = new ConnectionFactory()
                {
                    HostName = rabbitHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.BasicQos(0, 200, false);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (_, ea) =>
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    FanOutAsync(json, stoppingToken).GetAwaiter().GetResult();
                    _channel?.BasicAck(ea.DeliveryTag, false);
                };
                _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
                _log.LogInformation("World stream relay consuming from RabbitMQ at {host}/{queue}.", factory.HostName, QueueName);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "World stream relay failed to connect to RabbitMQ.");
            }

            return Task.CompletedTask;
        }

        async Task FanOutAsync(string json, CancellationToken ct)
        {
            WorldStreamMessage? msg = null;
            try
            {
                msg = JsonSerializer.Deserialize<WorldStreamMessage>(json, _jsonOpts);
            }
            catch
            {
                // Not a world stream message; ignore.
            }

            if (msg == null)
                return;

            foreach (var (socket, filter) in _clients)
            {
                if (socket.State != WebSocketState.Open)
                    continue;
                if (filter != null && !string.Equals(filter, msg.SessionId, StringComparison.OrdinalIgnoreCase))
                    continue;
                await SendRawAsync(socket, json, ct).ConfigureAwait(false);
            }
        }

        static async Task SendRawAsync(WebSocket socket, string json, CancellationToken ct)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            catch
            {
                // Client disconnected or closed; the endpoint's unsubscribe handles cleanup.
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _connection?.Close();
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
#endif
