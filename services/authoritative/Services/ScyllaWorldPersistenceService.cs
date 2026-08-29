#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Cassandra;
using Prometheus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services
{
    /// <summary>
    /// Persists generated world artifacts (rooms, enemies, loot) to ScyllaDB
    /// so the game client can query them via the /v1/world/ endpoints.
    ///
    /// Keyspace:  mmo_world
    /// Tables:    dungeon_sessions, dungeon_rooms, dungeon_enemies, dungeon_loot
    ///
    /// Enqueue a world with EnqueueWorld(); the background loop batches writes.
    /// </summary>
    public interface IScyllaWorldPersistenceService
    {
        void EnqueueWorld(PipelineExecutionRecord record);
        Task<WorldSessionRow?> GetSessionAsync(string sessionId, CancellationToken ct);
        Task<IReadOnlyList<WorldRoomRow>> GetRoomsAsync(string sessionId, CancellationToken ct);
        Task<IReadOnlyList<WorldEnemyRow>> GetEnemiesAsync(string sessionId, CancellationToken ct);
        Task<IReadOnlyList<WorldLootRow>> GetLootAsync(string sessionId, CancellationToken ct);
        Task<string?> GetEntitySnapshotAsync(string sessionId, string entityId, CancellationToken ct);
        Task<Dictionary<string, string>?> GetSessionMetadataAsync(string sessionId, CancellationToken ct);
        Task<bool> InsertEntitySnapshotAsync(string sessionId, string entityId, string entityType, string stateJson, int version = 1, int ttlSeconds = 0, CancellationToken ct = default);
        Task<bool> UpsertSessionMetadataAsync(string sessionId, System.Collections.Generic.IDictionary<string, string> properties, CancellationToken ct);
        Task<IReadOnlyList<string>> GetAllSessionIdsAsync(CancellationToken ct);
    }

    // ── Query result rows ────────────────────────────────────────────────────
    public sealed class WorldSessionRow
    {
        public string SessionId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public string PipelineId { get; set; } = string.Empty;
        public int Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int DungeonLevel { get; set; }
        public int RoomCount { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class WorldRoomRow
    {
        public int RoomId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class WorldEnemyRow
    {
        public int EnemyId { get; set; }
        public string Archetype { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
    }

    public sealed class WorldLootRow
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
    }

    // ── Service ──────────────────────────────────────────────────────────────
    public sealed class ScyllaWorldPersistenceService : BackgroundService, IScyllaWorldPersistenceService
    {
        // Prometheus metrics
        private static readonly Counter _snapshotInsertsTotal = Metrics.CreateCounter("scylla_snapshot_inserts_total", "Total number of snapshot inserts to Scylla", new CounterConfiguration
        {
            LabelNames = new[] { "entity_type" }
        });

        private static readonly Counter _snapshotInsertFailuresTotal = Metrics.CreateCounter("scylla_snapshot_insert_failures_total", "Snapshot insert failures to Scylla", new CounterConfiguration
        {
            LabelNames = new[] { "entity_type" }
        });

        private static readonly Histogram _snapshotInsertDurationSeconds = Metrics.CreateHistogram("scylla_snapshot_insert_duration_seconds", "Duration of snapshot inserts to Scylla in seconds");

        private static readonly Counter _metadataUpsertsTotal = Metrics.CreateCounter("scylla_metadata_upserts_total", "Total number of session metadata upserts to Scylla");

        private static readonly Counter _metadataUpsertFailuresTotal = Metrics.CreateCounter("scylla_metadata_upsert_failures_total", "Session metadata upsert failures to Scylla");

        private readonly string _contactPoint;
        private readonly ILogger<ScyllaWorldPersistenceService> _log;
        private readonly Channel<PipelineExecutionRecord> _queue;

        private ICluster? _cluster;
        private ISession? _session;
        private volatile bool _schemaReady;

        // Prepared statements — set once after schema init, then reused.
        private PreparedStatement? _psInsertSession;
        private PreparedStatement? _psInsertRoom;
        private PreparedStatement? _psInsertEnemy;
        private PreparedStatement? _psInsertLoot;
        private PreparedStatement? _psInsertEntitySnapshot;
        private PreparedStatement? _psInsertEntitySnapshotWithTtl;
        private PreparedStatement? _psUpsertSessionMetadata;

        public ScyllaWorldPersistenceService(
            IConfiguration config,
            ILogger<ScyllaWorldPersistenceService> log)
        {
            _contactPoint = config["SCYLLA_CONTACT_POINT"] ?? "scylla";
            _log = log;
            _queue = Channel.CreateBounded<PipelineExecutionRecord>(
                new BoundedChannelOptions(1_000)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        // ── IScyllaWorldPersistenceService ───────────────────────────────────

        public void EnqueueWorld(PipelineExecutionRecord record)
        {
            if (!_schemaReady) return;
            _queue.Writer.TryWrite(record);
        }

        public async Task<WorldSessionRow?> GetSessionAsync(string sessionId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return null;
            try
            {
                var rs = await _session.ExecuteAsync(
                    new SimpleStatement(
                        "SELECT session_id, execution_id, pipeline_id, seed, width, height," +
                        " dungeon_level, room_count, enemy_count, loot_count, created_at" +
                        " FROM mmo_world.dungeon_sessions WHERE session_id = ?",
                        sessionId))
                    .ConfigureAwait(false);

                var row = rs.FirstOrDefault();
                if (row == null) return null;

                return new WorldSessionRow
                {
                    SessionId    = row.GetValue<string>("session_id"),
                    ExecutionId  = row.GetValue<string>("execution_id"),
                    PipelineId   = row.GetValue<string>("pipeline_id"),
                    Seed         = row.GetValue<int>("seed"),
                    Width        = row.GetValue<int>("width"),
                    Height       = row.GetValue<int>("height"),
                    DungeonLevel = row.GetValue<int>("dungeon_level"),
                    RoomCount    = row.GetValue<int>("room_count"),
                    EnemyCount   = row.GetValue<int>("enemy_count"),
                    LootCount    = row.GetValue<int>("loot_count"),
                    CreatedAt    = row.GetValue<DateTimeOffset>("created_at")
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetSessionAsync failed for {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<string?> GetEntitySnapshotAsync(string sessionId, string entityId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return null;
            try
            {
                var rs = await _session.ExecuteAsync(new SimpleStatement(
                    "SELECT state_json FROM mmo_world.entity_snapshots WHERE session_id = ? AND entity_id = ?",
                    sessionId, entityId)).ConfigureAwait(false);

                var row = rs.FirstOrDefault();
                return row == null ? null : row.GetValue<string>("state_json");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetEntitySnapshotAsync failed for {SessionId}/{EntityId}", sessionId, entityId);
                return null;
            }
        }

        public async Task<Dictionary<string, string>?> GetSessionMetadataAsync(string sessionId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return null;
            try
            {
                var rs = await _session.ExecuteAsync(new SimpleStatement(
                    "SELECT properties FROM mmo_world.session_metadata WHERE session_id = ?",
                    sessionId)).ConfigureAwait(false);

                var row = rs.FirstOrDefault();
                if (row == null) return null;

                // Cassandra maps are returned as IDictionary<object, object>
                var map = row.GetValue<System.Collections.Generic.IDictionary<string, string>>("properties");
                return map == null ? null : new Dictionary<string, string>(map);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetSessionMetadataAsync failed for {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<bool> InsertEntitySnapshotAsync(string sessionId, string entityId, string entityType, string stateJson, int version = 1, int ttlSeconds = 0, CancellationToken ct = default)
        {
            if (_session == null || !_schemaReady || _psInsertEntitySnapshot == null) return false;
            try
            {
                using var timer = _snapshotInsertDurationSeconds.NewTimer();
                if (ttlSeconds > 0 && _psInsertEntitySnapshotWithTtl != null)
                {
                    await _session.ExecuteAsync(_psInsertEntitySnapshotWithTtl.Bind(
                        sessionId,
                        entityId,
                        entityType,
                        version,
                        stateJson,
                        DateTimeOffset.UtcNow,
                        ttlSeconds)).ConfigureAwait(false);
                }
                else
                {
                    await _session.ExecuteAsync(_psInsertEntitySnapshot.Bind(
                        sessionId,
                        entityId,
                        entityType,
                        version,
                        stateJson,
                        DateTimeOffset.UtcNow)).ConfigureAwait(false);
                }

                _snapshotInsertsTotal.WithLabels(entityType ?? "unknown").Inc();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "InsertEntitySnapshotAsync failed for {SessionId}/{EntityId}", sessionId, entityId);
                _snapshotInsertFailuresTotal.WithLabels(entityType ?? "unknown").Inc();
                return false;
            }
        }

        public async Task<bool> UpsertSessionMetadataAsync(string sessionId, System.Collections.Generic.IDictionary<string, string> properties, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psUpsertSessionMetadata == null) return false;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await _session.ExecuteAsync(_psUpsertSessionMetadata.Bind(sessionId, properties, DateTimeOffset.UtcNow)).ConfigureAwait(false);
                sw.Stop();
                _metadataUpsertsTotal.Inc();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "UpsertSessionMetadataAsync failed for {SessionId}", sessionId);
                _metadataUpsertFailuresTotal.Inc();
                return false;
            }
        }

        public async Task<IReadOnlyList<WorldRoomRow>> GetRoomsAsync(string sessionId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return Array.Empty<WorldRoomRow>();
            try
            {
                var rs = await _session.ExecuteAsync(
                    new SimpleStatement(
                        "SELECT room_id, x, y, width, height" +
                        " FROM mmo_world.dungeon_rooms WHERE session_id = ?",
                        sessionId))
                    .ConfigureAwait(false);

                return rs.Select(r => new WorldRoomRow
                {
                    RoomId = r.GetValue<int>("room_id"),
                    X      = r.GetValue<int>("x"),
                    Y      = r.GetValue<int>("y"),
                    Width  = r.GetValue<int>("width"),
                    Height = r.GetValue<int>("height")
                }).ToArray();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetRoomsAsync failed for {SessionId}", sessionId);
                return Array.Empty<WorldRoomRow>();
            }
        }

        public async Task<IReadOnlyList<WorldEnemyRow>> GetEnemiesAsync(string sessionId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return Array.Empty<WorldEnemyRow>();
            try
            {
                var rs = await _session.ExecuteAsync(
                    new SimpleStatement(
                        "SELECT enemy_id, archetype, x, y, level" +
                        " FROM mmo_world.dungeon_enemies WHERE session_id = ?",
                        sessionId))
                    .ConfigureAwait(false);

                return rs.Select(r => new WorldEnemyRow
                {
                    EnemyId   = r.GetValue<int>("enemy_id"),
                    Archetype = r.GetValue<string>("archetype"),
                    X         = r.GetValue<int>("x"),
                    Y         = r.GetValue<int>("y"),
                    Level     = r.GetValue<int>("level")
                }).ToArray();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetEnemiesAsync failed for {SessionId}", sessionId);
                return Array.Empty<WorldEnemyRow>();
            }
        }

        public async Task<IReadOnlyList<WorldLootRow>> GetLootAsync(string sessionId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return Array.Empty<WorldLootRow>();
            try
            {
                var rs = await _session.ExecuteAsync(
                    new SimpleStatement(
                        "SELECT item_id, item_type, tier, x, y" +
                        " FROM mmo_world.dungeon_loot WHERE session_id = ?",
                        sessionId))
                    .ConfigureAwait(false);

                return rs.Select(r => new WorldLootRow
                {
                    ItemId   = r.GetValue<string>("item_id"),
                    ItemType = r.GetValue<string>("item_type"),
                    Tier     = r.GetValue<string>("tier"),
                    X        = r.GetValue<int>("x"),
                    Y        = r.GetValue<int>("y")
                }).ToArray();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetLootAsync failed for {SessionId}", sessionId);
                return Array.Empty<WorldLootRow>();
            }
        }

        public async Task<IReadOnlyList<string>> GetAllSessionIdsAsync(CancellationToken ct)
        {
            if (_session == null || !_schemaReady) return Array.Empty<string>();
            try
            {
                var rs = await _session.ExecuteAsync(new SimpleStatement(
                    "SELECT session_id FROM mmo_world.dungeon_sessions")).ConfigureAwait(false);

                return rs.Select(r => r.GetValue<string>("session_id")).ToArray();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetAllSessionIdsAsync failed");
                return Array.Empty<string>();
            }
        }

        // ── BackgroundService ────────────────────────────────────────────────

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSchemaAsync(cancellationToken).ConfigureAwait(false);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var record = await _queue.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
                    await PersistWorldAsync(record, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogError(ex, "ScyllaWorldPersistenceService consumer loop error");
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false); }
                    catch { break; }
                }
            }
        }

        public override void Dispose()
        {
            _session?.Dispose();
            _cluster?.Dispose();
            base.Dispose();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task InitSchemaAsync(CancellationToken ct)
        {
            const int maxAttempts = 15;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _cluster = Cluster.Builder()
                        .AddContactPoint(_contactPoint)
                        .WithPort(9042)
                        .Build();

                    _session = await Task.Run(() => _cluster.Connect(), ct).ConfigureAwait(false);

                    // Keyspace — SimpleStrategy is fine for a single-node dev cluster.
                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE KEYSPACE IF NOT EXISTS mmo_world
                        WITH replication = {'class':'SimpleStrategy','replication_factor':1}
                        AND durable_writes = true"))
                        .ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(
                        "USE mmo_world")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS dungeon_sessions (
                            session_id    TEXT,
                            execution_id  TEXT,
                            pipeline_id   TEXT,
                            seed          INT,
                            width         INT,
                            height        INT,
                            dungeon_level INT,
                            room_count    INT,
                            enemy_count   INT,
                            loot_count    INT,
                            created_at    TIMESTAMP,
                            PRIMARY KEY (session_id)
                        )")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS dungeon_rooms (
                            session_id TEXT,
                            room_id    INT,
                            x          INT,
                            y          INT,
                            width      INT,
                            height     INT,
                            PRIMARY KEY (session_id, room_id)
                        )")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS dungeon_enemies (
                            session_id TEXT,
                            enemy_id   INT,
                            archetype  TEXT,
                            x          INT,
                            y          INT,
                            level      INT,
                            PRIMARY KEY (session_id, enemy_id)
                        )")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS dungeon_loot (
                            session_id TEXT,
                            item_id    TEXT,
                            item_type  TEXT,
                            tier       TEXT,
                            x          INT,
                            y          INT,
                            PRIMARY KEY (session_id, item_id)
                        )")).ConfigureAwait(false);

                    // Prepare statements once — avoids repeated CQL parsing per insert.
                    _psInsertSession = await _session.PrepareAsync(@"
                        INSERT INTO dungeon_sessions
                            (session_id, execution_id, pipeline_id, seed, width, height,
                             dungeon_level, room_count, enemy_count, loot_count, created_at)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psInsertRoom = await _session.PrepareAsync(@"
                        INSERT INTO dungeon_rooms (session_id, room_id, x, y, width, height)
                        VALUES (?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psInsertEnemy = await _session.PrepareAsync(@"
                        INSERT INTO dungeon_enemies (session_id, enemy_id, archetype, x, y, level)
                        VALUES (?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psInsertLoot = await _session.PrepareAsync(@"
                        INSERT INTO dungeon_loot (session_id, item_id, item_type, tier, x, y)
                        VALUES (?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psInsertEntitySnapshot = await _session.PrepareAsync(@"
                        INSERT INTO entity_snapshots (session_id, entity_id, entity_type, version, state_json, last_updated)
                        VALUES (?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psInsertEntitySnapshotWithTtl = await _session.PrepareAsync(@"
                        INSERT INTO entity_snapshots (session_id, entity_id, entity_type, version, state_json, last_updated)
                        VALUES (?, ?, ?, ?, ?, ?) USING TTL ?").ConfigureAwait(false);

                    _psUpsertSessionMetadata = await _session.PrepareAsync(@"
                        INSERT INTO session_metadata (session_id, properties, last_updated)
                        VALUES (?, ?, ?)").ConfigureAwait(false);

                    _schemaReady = true;
                    _log.LogInformation("ScyllaDB mmo_world schema initialized on {ContactPoint}", _contactPoint);
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _log.LogWarning(
                        "ScyllaDB schema init attempt {Attempt}/{Max} failed: {Msg}",
                        attempt, maxAttempts, ex.Message);
                    try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); }
                    catch { return; }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "ScyllaDB schema init failed after {Max} attempts — world persistence disabled", maxAttempts);
                }
            }
        }

        private async Task PersistWorldAsync(PipelineExecutionRecord record, CancellationToken ct)
        {
            if (_session == null || _psInsertSession == null) return;

            var world   = record.World;
            var session = record.SessionId ?? record.ExecutionId;

            try
            {
                // Session row
                await _session.ExecuteAsync(
                    _psInsertSession.Bind(
                        session,
                        record.ExecutionId,
                        record.PipelineId,
                        world.Seed,
                        world.Width,
                        world.Height,
                        world.DungeonLevel,
                        world.Rooms.Count,
                        world.Enemies.Count,
                        world.Loot.Count,
                        DateTimeOffset.UtcNow))
                    .ConfigureAwait(false);

                // Rooms
                var roomTasks = world.Rooms.Select(r =>
                    _session.ExecuteAsync(
                        _psInsertRoom!.Bind(session, r.Id, r.X, r.Y, r.Width, r.Height)));
                await Task.WhenAll(roomTasks).ConfigureAwait(false);

                // Enemies
                var enemyTasks = world.Enemies.Select(e =>
                    _session.ExecuteAsync(
                        _psInsertEnemy!.Bind(session, e.Id, e.Archetype, e.X, e.Y, e.Level)));
                await Task.WhenAll(enemyTasks).ConfigureAwait(false);

                // Entity snapshots (store a JSON snapshot of enemy state for quick lookups)
                if (_psInsertEntitySnapshot != null)
                {
                    var snapshotTasks = world.Enemies.Select(e =>
                        InsertEntitySnapshotAsync(
                            session,
                            e.Id.ToString(),
                            "enemy",
                            System.Text.Json.JsonSerializer.Serialize(new { e.Id, e.Archetype, e.X, e.Y, e.Level }),
                            version: 1,
                            ttlSeconds: 0,
                            ct));
                    await Task.WhenAll(snapshotTasks).ConfigureAwait(false);
                }

                // Loot
                var lootTasks = world.Loot.Select(l =>
                    _session.ExecuteAsync(
                        _psInsertLoot!.Bind(session, l.ItemId, l.ItemType, l.Tier, l.X, l.Y)));
                await Task.WhenAll(lootTasks).ConfigureAwait(false);

                _log.LogInformation(
                    "Persisted world {ExecutionId} to ScyllaDB: session={Session} " +
                    "rooms={Rooms} enemies={Enemies} loot={Loot}",
                    record.ExecutionId, session,
                    world.Rooms.Count, world.Enemies.Count, world.Loot.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Failed to persist world {ExecutionId} to ScyllaDB", record.ExecutionId);
            }
        }
    }
}
#endif
