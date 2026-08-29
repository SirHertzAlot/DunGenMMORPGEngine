#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Authoritative.Services
{
    public interface IWorldEventPersistenceService
    {
        void EnqueueEvent(WorldSessionEvent evt);
        Task<IReadOnlyList<WorldSessionEvent>> QueryEventsAsync(string sessionId, int take, CancellationToken cancellationToken);
        Task<WorldSessionSummary> GetSessionSummaryAsync(string sessionId, CancellationToken cancellationToken);
    }

    public sealed class WorldSessionSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public long TotalEvents { get; set; }
        public long EntitySnapshotCount { get; set; }
        public long SystemEventCount { get; set; }
        public int TurnCount { get; set; }
        public DateTime? FirstEventUtc { get; set; }
        public DateTime? LastEventUtc { get; set; }
    }

    public sealed class WorldEventPersistenceService : BackgroundService, IWorldEventPersistenceService
    {
        private readonly Channel<WorldSessionEvent> _queue;
        private readonly string _pgConnStr;
        private readonly ILogger<WorldEventPersistenceService> _log;
        private volatile bool _schemaReady;

        private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

        public WorldEventPersistenceService(IConfiguration config, ILogger<WorldEventPersistenceService> log)
        {
            _pgConnStr = config["POSTGRES_CONNECTION_STRING"]
                ?? "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb";
            _log = log;
            _queue = Channel.CreateBounded<WorldSessionEvent>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public void EnqueueEvent(WorldSessionEvent evt)
        {
            if (!_schemaReady) return;
            _queue.Writer.TryWrite(evt);
        }

        public async Task<IReadOnlyList<WorldSessionEvent>> QueryEventsAsync(
            string sessionId, int take, CancellationToken cancellationToken)
        {
            if (!_schemaReady) return Array.Empty<WorldSessionEvent>();

            var results = new List<WorldSessionEvent>();
            try
            {
                await using var conn = new NpgsqlConnection(_pgConnStr);
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT event_id, session_id, event_type, category, frame,
                           entity_id, message, data::text, timestamp_utc
                    FROM world_session_events
                    WHERE session_id = @sid
                    ORDER BY timestamp_utc DESC
                    LIMIT @take";
                cmd.Parameters.AddWithValue("sid", sessionId);
                cmd.Parameters.AddWithValue("take", Math.Clamp(take, 1, 1000));

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var dataJson = reader.IsDBNull(7) ? "{}" : reader.GetString(7);
                    Dictionary<string, string> data;
                    try
                    {
                        data = JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson, _jsonOpts)
                               ?? new Dictionary<string, string>(StringComparer.Ordinal);
                    }
                    catch
                    {
                        data = new Dictionary<string, string>(StringComparer.Ordinal);
                    }

                    results.Add(new WorldSessionEvent
                    {
                        EventId = reader.GetString(0),
                        SessionId = reader.GetString(1),
                        EventType = reader.GetString(2),
                        Category = reader.GetString(3),
                        Frame = (uint)reader.GetInt32(4),
                        EntityId = reader.GetString(5),
                        Message = reader.GetString(6),
                        Data = data,
                        TimestampUtc = DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc),
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to query world_session_events for session {SessionId}", sessionId);
            }
            return results;
        }

        public async Task<WorldSessionSummary> GetSessionSummaryAsync(
            string sessionId, CancellationToken cancellationToken)
        {
            var summary = new WorldSessionSummary { SessionId = sessionId };
            if (!_schemaReady) return summary;

            try
            {
                await using var conn = new NpgsqlConnection(_pgConnStr);
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        COUNT(*) AS total,
                        COUNT(*) FILTER (WHERE event_type = 'entity.state.snapshot') AS snapshots,
                        COUNT(*) FILTER (WHERE event_type LIKE 'system.%') AS system_events,
                        COALESCE(MAX(CAST(NULLIF(data->>'turn', '') AS INTEGER)), 0) AS max_turn,
                        MIN(timestamp_utc) AS first_ts,
                        MAX(timestamp_utc) AS last_ts
                    FROM world_session_events
                    WHERE session_id = @sid";
                cmd.Parameters.AddWithValue("sid", sessionId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    summary.TotalEvents = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    summary.EntitySnapshotCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    summary.SystemEventCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    summary.TurnCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    if (!reader.IsDBNull(4))
                        summary.FirstEventUtc = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc);
                    if (!reader.IsDBNull(5))
                        summary.LastEventUtc = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to query session summary for {SessionId}", sessionId);
            }
            return summary;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSchemaAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<WorldSessionEvent>(64);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    batch.Clear();
                    using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timerCts.CancelAfter(TimeSpan.FromMilliseconds(200));

                    try
                    {
                        while (batch.Count < 64)
                        {
                            var evt = await _queue.Reader.ReadAsync(timerCts.Token);
                            batch.Add(evt);
                        }
                    }
                    catch (OperationCanceledException) { /* timer expired or service stopping — flush what we have */ }

                    if (batch.Count > 0 && !stoppingToken.IsCancellationRequested)
                        await FlushBatchAsync(batch, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogError(ex, "WorldEventPersistenceService consumer loop error; pausing 5s");
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { break; }
                }
            }
        }

        private async Task FlushBatchAsync(IReadOnlyList<WorldSessionEvent> batch, CancellationToken ct)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_pgConnStr);
                await conn.OpenAsync(ct);
                await using var tx = await conn.BeginTransactionAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO world_session_events
                        (event_id, session_id, event_type, category, frame, entity_id, message, data, timestamp_utc)
                    VALUES
                        (@eid, @sid, @etype, @cat, @frame, @entity, @msg, @data::jsonb, @ts)
                    ON CONFLICT (event_id) DO NOTHING";

                var pEid    = cmd.Parameters.Add("eid",    NpgsqlDbType.Text);
                var pSid    = cmd.Parameters.Add("sid",    NpgsqlDbType.Text);
                var pEtype  = cmd.Parameters.Add("etype",  NpgsqlDbType.Text);
                var pCat    = cmd.Parameters.Add("cat",    NpgsqlDbType.Text);
                var pFrame  = cmd.Parameters.Add("frame",  NpgsqlDbType.Integer);
                var pEntity = cmd.Parameters.Add("entity", NpgsqlDbType.Text);
                var pMsg    = cmd.Parameters.Add("msg",    NpgsqlDbType.Text);
                var pData   = cmd.Parameters.Add("data",   NpgsqlDbType.Text);
                var pTs     = cmd.Parameters.Add("ts",     NpgsqlDbType.TimestampTz);
                await cmd.PrepareAsync(ct);

                foreach (var evt in batch)
                {
                    pEid.Value    = evt.EventId;
                    pSid.Value    = evt.SessionId;
                    pEtype.Value  = evt.EventType;
                    pCat.Value    = evt.Category;
                    pFrame.Value  = (int)evt.Frame;
                    pEntity.Value = evt.EntityId;
                    pMsg.Value    = evt.Message;
                    pData.Value   = JsonSerializer.Serialize(evt.Data, _jsonOpts);
                    pTs.Value     = evt.TimestampUtc == default ? DateTime.UtcNow : evt.TimestampUtc;
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                _log.LogDebug("Persisted {Count} world session events to Postgres", batch.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to flush {Count} world session events to Postgres", batch.Count);
            }
        }

        private async Task InitSchemaAsync(CancellationToken ct)
        {
            const int maxAttempts = 10;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(_pgConnStr);
                    await conn.OpenAsync(ct);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS world_session_events (
                            event_id    TEXT        NOT NULL PRIMARY KEY,
                            session_id  TEXT        NOT NULL,
                            event_type  TEXT        NOT NULL,
                            category    TEXT        NOT NULL DEFAULT '',
                            frame       INTEGER     NOT NULL DEFAULT 0,
                            entity_id   TEXT        NOT NULL DEFAULT '',
                            message     TEXT        NOT NULL DEFAULT '',
                            data        JSONB       NOT NULL DEFAULT '{}',
                            timestamp_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
                        );
                        CREATE INDEX IF NOT EXISTS idx_wse_session_ts
                            ON world_session_events(session_id, timestamp_utc DESC);
                        CREATE INDEX IF NOT EXISTS idx_wse_event_type
                            ON world_session_events(event_type);
                        CREATE INDEX IF NOT EXISTS idx_wse_frame
                            ON world_session_events(session_id, frame);";
                    await cmd.ExecuteNonQueryAsync(ct);
                    _schemaReady = true;
                    _log.LogInformation("world_session_events schema initialized");
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _log.LogWarning(
                        "world_session_events schema init attempt {Attempt}/{Max} failed: {Msg}",
                        attempt, maxAttempts, ex.Message);
                    try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch { return; }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to initialize world_session_events schema after {Max} attempts", maxAttempts);
                }
            }
        }
    }
}
#endif
