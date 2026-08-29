#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace Authoritative.Services
{
    public interface IDatabaseObservabilityService
    {
        Task<DatabaseObservabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
        Task<DatabaseMaintenanceResult> RunMaintenanceAsync(string database, DatabaseMaintenanceRequest request, CancellationToken cancellationToken);
        Task<PrometheusQueryResult> QueryAsync(string database, string promQl, CancellationToken cancellationToken);
        Task<RedisKeyValueResult> GetRedisKeyAsync(string key, CancellationToken cancellationToken);
        Task<RedisKeyMutationResult> SetRedisKeyAsync(RedisKeyMutationRequest request, CancellationToken cancellationToken);
        Task<RedisKeyMutationResult> DeleteRedisKeyAsync(string key, CancellationToken cancellationToken);
    }

    public sealed class DatabaseObservabilityService : IDatabaseObservabilityService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseObservabilityService> _log;
        private readonly string _prometheusBaseUrl;

        public DatabaseObservabilityService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DatabaseObservabilityService> log)
        {
            _httpClientFactory = httpClientFactory;
            _log = log;
            _prometheusBaseUrl = configuration["PROMETHEUS_BASE_URL"] ?? "http://prometheus:9090";
        }

        public async Task<DatabaseObservabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            var redisTask = BuildRedisPanelAsync(cancellationToken);
            var postgresTask = BuildPostgresPanelAsync(cancellationToken);
            var scyllaTask = BuildScyllaPanelAsync(cancellationToken);
            await Task.WhenAll(redisTask, postgresTask, scyllaTask);

            return new DatabaseObservabilitySnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                Databases = new[]
                {
                    await redisTask,
                    await postgresTask,
                    await scyllaTask
                }
            };
        }

        public async Task<PrometheusQueryResult> QueryAsync(string database, string promQl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(promQl))
            {
                return new PrometheusQueryResult
                {
                    Database = NormalizeDatabase(database),
                    Query = string.Empty,
                    Success = false,
                    Message = "query is required"
                };
            }

            try
            {
                var value = await QueryScalarAsync(promQl, cancellationToken);
                return new PrometheusQueryResult
                {
                    Database = NormalizeDatabase(database),
                    Query = promQl,
                    Value = value,
                    Success = value.HasValue,
                    CapturedAtUtc = DateTime.UtcNow,
                    Message = value.HasValue ? "ok" : "no scalar datapoint returned"
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Prometheus query failed for {database}: {query}", database, promQl);
                return new PrometheusQueryResult
                {
                    Database = NormalizeDatabase(database),
                    Query = promQl,
                    Success = false,
                    Message = ex.Message,
                    CapturedAtUtc = DateTime.UtcNow
                };
            }
        }

        public async Task<DatabaseMaintenanceResult> RunMaintenanceAsync(string database, DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
        {
            var normalizedDb = NormalizeDatabase(database);
            var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(action))
            {
                return new DatabaseMaintenanceResult
                {
                    Database = normalizedDb,
                    Action = string.Empty,
                    Success = false,
                    Message = "action is required"
                };
            }

            return normalizedDb switch
            {
                "redis" => await RunRedisMaintenanceAsync(action, request, cancellationToken),
                "postgres" => await RunPostgresMaintenanceAsync(action, request, cancellationToken),
                "scylla" => await RunScyllaMaintenanceAsync(action, request, cancellationToken),
                _ => new DatabaseMaintenanceResult
                {
                    Database = normalizedDb,
                    Action = action,
                    Success = false,
                    Message = "unsupported database"
                }
            };
        }

        public async Task<RedisKeyValueResult> GetRedisKeyAsync(string key, CancellationToken cancellationToken)
        {
            var normalizedKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return new RedisKeyValueResult
                {
                    Key = string.Empty,
                    Exists = false,
                    Success = false,
                    Message = "key is required"
                };
            }

            try
            {
                using var mux = await ConnectionMultiplexer.ConnectAsync("redis:6379,abortConnect=false,connectTimeout=5000");
                var db = mux.GetDatabase();
                var value = await db.StringGetAsync(normalizedKey);
                var ttl = await db.KeyTimeToLiveAsync(normalizedKey);

                return new RedisKeyValueResult
                {
                    Key = normalizedKey,
                    Exists = value.HasValue,
                    Value = value.HasValue ? value.ToString() : string.Empty,
                    TimeToLiveSeconds = ttl?.TotalSeconds,
                    Success = true,
                    Message = value.HasValue ? "ok" : "key not found"
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Redis get key failed: {key}", normalizedKey);
                return new RedisKeyValueResult
                {
                    Key = normalizedKey,
                    Exists = false,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<RedisKeyMutationResult> SetRedisKeyAsync(RedisKeyMutationRequest request, CancellationToken cancellationToken)
        {
            var normalizedKey = (request.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return new RedisKeyMutationResult
                {
                    Key = string.Empty,
                    Success = false,
                    Message = "key is required"
                };
            }

            try
            {
                using var mux = await ConnectionMultiplexer.ConnectAsync("redis:6379,abortConnect=false,connectTimeout=5000");
                var db = mux.GetDatabase();
                var ttl = request.TimeToLiveSeconds.HasValue && request.TimeToLiveSeconds.Value > 0
                    ? TimeSpan.FromSeconds(request.TimeToLiveSeconds.Value)
                    : (TimeSpan?)null;

                var saved = await db.StringSetAsync(normalizedKey, request.Value ?? string.Empty, ttl);
                return new RedisKeyMutationResult
                {
                    Key = normalizedKey,
                    Success = saved,
                    Message = saved ? "key saved" : "set failed"
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Redis set key failed: {key}", normalizedKey);
                return new RedisKeyMutationResult
                {
                    Key = normalizedKey,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<RedisKeyMutationResult> DeleteRedisKeyAsync(string key, CancellationToken cancellationToken)
        {
            var normalizedKey = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return new RedisKeyMutationResult
                {
                    Key = string.Empty,
                    Success = false,
                    Message = "key is required"
                };
            }

            try
            {
                using var mux = await ConnectionMultiplexer.ConnectAsync("redis:6379,abortConnect=false,connectTimeout=5000");
                var db = mux.GetDatabase();
                var deleted = await db.KeyDeleteAsync(normalizedKey);
                return new RedisKeyMutationResult
                {
                    Key = normalizedKey,
                    Success = deleted,
                    Message = deleted ? "key deleted" : "key not found"
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Redis delete key failed: {key}", normalizedKey);
                return new RedisKeyMutationResult
                {
                    Key = normalizedKey,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<DatabasePanelSnapshot> BuildRedisPanelAsync(CancellationToken cancellationToken)
        {
            var metrics = await BuildMetricsAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["up"] = "max(up{job=\"redis\"})",
                ["exporter_up"] = "max(redis_up)",
                ["connected_clients"] = "max(redis_connected_clients)",
                ["memory_used_bytes"] = "max(redis_memory_used_bytes)",
                ["ops_per_sec_5m"] = "sum(rate(redis_commands_processed_total[5m]))"
            }, cancellationToken);

            var isUp = IsUp(metrics, "up") || IsUp(metrics, "exporter_up");

            return new DatabasePanelSnapshot
            {
                Name = "redis",
                DisplayName = "Redis",
                IsUp = isUp,
                CapturedAtUtc = DateTime.UtcNow,
                Metrics = metrics,
                MaintenanceActions = new[] { "ping", "memory-purge", "bgsave" },
                Notes = "Exporter-backed metrics with safe maintenance actions."
            };
        }

        private async Task<DatabasePanelSnapshot> BuildPostgresPanelAsync(CancellationToken cancellationToken)
        {
            var metrics = await BuildMetricsAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["up"] = "max(up{job=\"postgres\"})",
                ["exporter_up"] = "max(pg_up)",
                ["active_backends"] = "sum(pg_stat_database_numbackends)",
                ["commits_per_sec_5m"] = "sum(rate(pg_stat_database_xact_commit[5m]))",
                ["rollbacks_per_sec_5m"] = "sum(rate(pg_stat_database_xact_rollback[5m]))"
            }, cancellationToken);

            var isUp = IsUp(metrics, "up") || IsUp(metrics, "exporter_up");

            return new DatabasePanelSnapshot
            {
                Name = "postgres",
                DisplayName = "Postgres",
                IsUp = isUp,
                CapturedAtUtc = DateTime.UtcNow,
                Metrics = metrics,
                MaintenanceActions = new[] { "analyze", "vacuum", "checkpoint" },
                Notes = "Metrics are read from postgres-exporter via Prometheus."
            };
        }

        private async Task<DatabasePanelSnapshot> BuildScyllaPanelAsync(CancellationToken cancellationToken)
        {
            var metrics = await BuildMetricsAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["up"] = "max(up{job=\"scylla\"})",
                ["scrape_samples"] = "max(scrape_samples_scraped{job=\"scylla\"})",
                ["reads_per_sec_5m"] = "sum(rate(scylla_storage_proxy_coordinator_reads[5m]))",
                ["writes_per_sec_5m"] = "sum(rate(scylla_storage_proxy_coordinator_writes[5m]))"
            }, cancellationToken);

            return new DatabasePanelSnapshot
            {
                Name = "scylla",
                DisplayName = "ScyllaDB",
                IsUp = IsUp(metrics, "up"),
                CapturedAtUtc = DateTime.UtcNow,
                Metrics = metrics,
                MaintenanceActions = new[] { "compact", "cleanup" },
                Notes = "Maintenance actions use Scylla REST API on port 10000."
            };
        }

        private async Task<Dictionary<string, double?>> BuildMetricsAsync(
            IReadOnlyDictionary<string, string> expressions,
            CancellationToken cancellationToken)
        {
            var tasks = expressions.ToDictionary(
                pair => pair.Key,
                pair => QueryScalarSafeAsync(pair.Value, cancellationToken),
                StringComparer.Ordinal);

            await Task.WhenAll(tasks.Values);

            return tasks.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Result,
                StringComparer.Ordinal);
        }

        private async Task<double?> QueryScalarSafeAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                return await QueryScalarAsync(query, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Prometheus scalar query failed: {query}", query);
                return null;
            }
        }

        private async Task<double?> QueryScalarAsync(string query, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{_prometheusBaseUrl.TrimEnd('/')}/api/v1/query?query={encodedQuery}";

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);

            if (!doc.RootElement.TryGetProperty("status", out var statusElement) ||
                !string.Equals(statusElement.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array ||
                resultElement.GetArrayLength() == 0)
            {
                return null;
            }

            var first = resultElement[0];
            if (!first.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.Array ||
                valueElement.GetArrayLength() < 2)
            {
                return null;
            }

            var scalarText = valueElement[1].GetString();
            if (string.IsNullOrWhiteSpace(scalarText))
                return null;

            if (double.TryParse(scalarText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return null;
        }

        private async Task<DatabaseMaintenanceResult> RunRedisMaintenanceAsync(string action, DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                using var mux = await ConnectionMultiplexer.ConnectAsync("redis:6379,abortConnect=false,connectTimeout=5000");
                var db = mux.GetDatabase();
                var server = mux.GetServer("redis", 6379);

                return action switch
                {
                    "ping" => new DatabaseMaintenanceResult
                    {
                        Database = "redis",
                        Action = action,
                        Success = true,
                        Message = $"PONG in {await db.PingAsync()}"
                    },
                    "memory-purge" => await ExecuteRedisCommandAsync(db, action, "MEMORY", "PURGE"),
                    "bgsave" => await ExecuteRedisBgsaveAsync(server, action),
                    _ => UnsupportedAction("redis", action)
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Redis maintenance action failed: {action}", action);
                return FailedResult("redis", action, ex.Message);
            }
        }

        private static async Task<DatabaseMaintenanceResult> ExecuteRedisCommandAsync(IDatabase db, string action, string command, params object[] args)
        {
            var result = await db.ExecuteAsync(command, args);
            return new DatabaseMaintenanceResult
            {
                Database = "redis",
                Action = action,
                Success = true,
                Message = result.ToString() ?? "ok"
            };
        }

        private static async Task<DatabaseMaintenanceResult> ExecuteRedisBgsaveAsync(IServer server, string action)
        {
            await server.SaveAsync(SaveType.BackgroundSave);
            return new DatabaseMaintenanceResult
            {
                Database = "redis",
                Action = action,
                Success = true,
                Message = "background save requested"
            };
        }

        private static bool RequiresConfirmation(string action)
        {
            return action is "vacuum" or "checkpoint" or "memory-purge" or "bgsave" or "compact" or "cleanup";
        }

        private static bool IsUp(IReadOnlyDictionary<string, double?> metrics, string key)
        {
            return metrics.TryGetValue(key, out var value) && value.HasValue && value.Value >= 1d;
        }

        private async Task<DatabaseMaintenanceResult> RunPostgresMaintenanceAsync(string action, DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
        {
            if (RequiresConfirmation(action) && !request.Confirmed)
                return ConfirmationRequired("postgres", action);

            try
            {
                var connectionString = "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb;Timeout=5;Command Timeout=120";
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cancellationToken);

                var sql = action switch
                {
                    "analyze" => "ANALYZE;",
                    "vacuum" => "VACUUM (ANALYZE);",
                    "checkpoint" => "CHECKPOINT;",
                    _ => null
                };

                if (sql == null)
                    return UnsupportedAction("postgres", action);

                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                return new DatabaseMaintenanceResult
                {
                    Database = "postgres",
                    Action = action,
                    Success = true,
                    Message = "command completed"
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Postgres maintenance action failed: {action}", action);
                return FailedResult("postgres", action, ex.Message);
            }
        }

        private async Task<DatabaseMaintenanceResult> RunScyllaMaintenanceAsync(string action, DatabaseMaintenanceRequest request, CancellationToken cancellationToken)
        {
            if (RequiresConfirmation(action) && !request.Confirmed)
                return ConfirmationRequired("scylla", action);

            string? endpoint = action switch
            {
                "compact" => "/storage_service/compact",
                "cleanup" => "/storage_service/cleanup_all",
                _ => null
            };

            if (endpoint == null)
                return UnsupportedAction("scylla", action);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

                var client = _httpClientFactory.CreateClient();
                using var response = await client.PostAsync($"http://scylla:10000{endpoint}", content: null, cancellationToken: timeoutCts.Token);
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                var summary = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "ok" : body.Trim();

                return new DatabaseMaintenanceResult
                {
                    Database = "scylla",
                    Action = action,
                    Success = response.IsSuccessStatusCode,
                    Message = summary.Length > 240 ? summary[..240] : summary
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Scylla maintenance action failed: {action}", action);
                return FailedResult("scylla", action, ex.Message);
            }
        }

        private static DatabaseMaintenanceResult UnsupportedAction(string database, string action)
        {
            return new DatabaseMaintenanceResult
            {
                Database = database,
                Action = action,
                Success = false,
                Message = "unsupported action"
            };
        }

        private static DatabaseMaintenanceResult FailedResult(string database, string action, string message)
        {
            return new DatabaseMaintenanceResult
            {
                Database = database,
                Action = action,
                Success = false,
                Message = message
            };
        }

        private static DatabaseMaintenanceResult ConfirmationRequired(string database, string action)
        {
            return new DatabaseMaintenanceResult
            {
                Database = database,
                Action = action,
                Success = false,
                Message = "confirmation required for this action"
            };
        }

        private static string NormalizeDatabase(string database)
        {
            return (database ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "redis" => "redis",
                "postgres" => "postgres",
                "postgresql" => "postgres",
                "scylla" => "scylla",
                "scylladb" => "scylla",
                _ => (database ?? string.Empty).Trim().ToLowerInvariant()
            };
        }
    }

    public sealed class DatabaseObservabilitySnapshot
    {
        public DateTime CapturedAtUtc { get; set; }
        public IReadOnlyCollection<DatabasePanelSnapshot> Databases { get; set; } = Array.Empty<DatabasePanelSnapshot>();
    }

    public sealed class DatabasePanelSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsUp { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public IReadOnlyDictionary<string, double?> Metrics { get; set; } = new Dictionary<string, double?>(StringComparer.Ordinal);
        public IReadOnlyCollection<string> MaintenanceActions { get; set; } = Array.Empty<string>();
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class DatabaseMaintenanceRequest
    {
        public string Action { get; set; } = string.Empty;
        public bool Confirmed { get; set; }
    }

    public sealed class DatabaseMaintenanceResult
    {
        public string Database { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PrometheusQueryResult
    {
        public string Database { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public bool Success { get; set; }
        public double? Value { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
    }

    public sealed class RedisKeyMutationRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public long? TimeToLiveSeconds { get; set; }
    }

    public sealed class RedisKeyValueResult
    {
        public string Key { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string Value { get; set; } = string.Empty;
        public double? TimeToLiveSeconds { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class RedisKeyMutationResult
    {
        public string Key { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
#endif
