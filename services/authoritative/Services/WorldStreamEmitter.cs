#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Authoritative.Services
{
    /// <summary>
    /// High-throughput emitter for near-real-time world/action streaming.
    /// Each call publishes the message to RabbitMQ (live fan-out to the WebSocket
    /// relay / game clients) AND buffers it into Redis (hot cache) so a client that
    /// reconnects or a hiccup in the broker does not miss recent frames.
    /// Postgres/ScyllaDB remain the durable stores; this pipe is the hot path.
    /// </summary>
    public interface IWorldStreamEmitter
    {
        Task EmitAsync(WorldStreamMessage msg, CancellationToken cancellationToken = default);
    }

    public sealed class WorldStreamEmitter : IWorldStreamEmitter
    {
        const string QueueName = "world.events";

        static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };

        readonly ILogger<WorldStreamEmitter> _log;
        readonly bool _enabled;
        readonly int _redisBufferCapacity;
        readonly TimeSpan _redisBufferTtl;
        ConnectionFactory? _connectionFactory;
        IConnection? _rabbitConnection;
        IModel? _channel;
        IDatabase? _redis;
        readonly SemaphoreSlim _initLock = new(1, 1);

        public WorldStreamEmitter(IConfiguration config, ILogger<WorldStreamEmitter> log)
        {
            _log = log;
            _enabled = string.Equals(config["WORLD_STREAM_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
            _redisBufferCapacity = int.TryParse(config["WORLD_STREAM_BUFFER_CAPACITY"], out var cap) ? Math.Clamp(cap, 50, 5000) : 500;
            _redisBufferTtl = TimeSpan.FromMinutes(
                int.TryParse(config["WORLD_STREAM_BUFFER_TTL_MINUTES"], out var ttl) ? Math.Clamp(ttl, 1, 1440) : 60);

            if (_enabled)
            {
                var rabbitHost = string.IsNullOrWhiteSpace(config["RABBITMQ_HOST"])
                    ? Environment.GetEnvironmentVariable("RABBITMQ_HOST")
                    : config["RABBITMQ_HOST"];
                _connectionFactory = new ConnectionFactory
                {
                    HostName = string.IsNullOrWhiteSpace(rabbitHost) ? "rabbitmq" : rabbitHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                    RequestedHeartbeat = TimeSpan.FromSeconds(30),
                };

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
                    _log.LogWarning(ex, "World stream Redis buffer unavailable; continuing without hot cache.");
                }
            }
        }

        public async Task EmitAsync(WorldStreamMessage msg, CancellationToken cancellationToken = default)
        {
            if (!_enabled)
                return;

            var json = JsonSerializer.Serialize(msg, _jsonOpts);

            try
            {
                await EnsureBrokerAsync(cancellationToken).ConfigureAwait(false);
                var body = Encoding.UTF8.GetBytes(json);
                _channel?.BasicPublish(
                    exchange: string.Empty,
                    routingKey: QueueName,
                    basicProperties: null,
                    body: body);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "RabbitMQ publish failed for {type}; hot cache buffer will still capture the event.", msg.Type);
            }

            try
            {
                await BufferAsync(msg, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Redis hot-cache buffer write failed for session {SessionId}; event not retained.", msg.SessionId);
            }
        }

        Task EnsureBrokerAsync(CancellationToken cancellationToken)
        {
            if (_connectionFactory == null || _channel != null)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    _initLock.Wait(cancellationToken);
                    try
                    {
                        if (_channel != null)
                            return;

                        _rabbitConnection = _connectionFactory.CreateConnection();
                        _channel = _rabbitConnection.CreateModel();
                        _channel.QueueDeclare(
                            queue: QueueName,
                            durable: true,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);
                        _log.LogInformation("World stream publisher connected to RabbitMQ at {host}.", _connectionFactory.HostName);
                    }
                    finally
                    {
                        _initLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to connect world stream publisher to RabbitMQ. Will retry on next emit.");
                }
            }, cancellationToken);
        }

        async Task BufferAsync(WorldStreamMessage msg, string json)
        {
            if (_redis == null)
                return;

            var listKey = $"ws:buffer:{msg.SessionId}";
            await _redis.ListRightPushAsync(listKey, json).ConfigureAwait(false);
            await _redis.ListTrimAsync(listKey, -_redisBufferCapacity, -1).ConfigureAwait(false);
            await _redis.KeyExpireAsync(listKey, _redisBufferTtl).ConfigureAwait(false);
        }
    }
}
#endif
