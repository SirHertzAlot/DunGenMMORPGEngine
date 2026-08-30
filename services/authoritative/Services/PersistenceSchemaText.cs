#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Services
{
    /// <summary>
    /// Canonical DDL and write-statement text for every database surface in the
    /// authoritative backend, so the schema used at runtime can never drift from
    /// the schema documented in db/scylla/mmo_world.cql and db/migrations/.
    /// PersistenceContractTests enforces DDL &lt;-&gt; INSERT &lt;-&gt; row-model lockstep.
    /// </summary>
    public static class PersistenceSchemaText
    {
        // ── Scylla/Cassandra: keyspace mmo_world ──────────────────────────────

        public const string MmoWorldKeyspaceDdl = @"
            CREATE KEYSPACE IF NOT EXISTS mmo_world
            WITH replication = {'class':'SimpleStrategy','replication_factor':1}
            AND durable_writes = true";

        public const string DungeonSessionsDdl = @"
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
            )";

        public const string DungeonRoomsDdl = @"
            CREATE TABLE IF NOT EXISTS dungeon_rooms (
                session_id TEXT,
                room_id    INT,
                x          INT,
                y          INT,
                width      INT,
                height     INT,
                PRIMARY KEY (session_id, room_id)
            )";

        public const string DungeonEnemiesDdl = @"
            CREATE TABLE IF NOT EXISTS dungeon_enemies (
                session_id TEXT,
                enemy_id   INT,
                archetype  TEXT,
                x          INT,
                y          INT,
                level      INT,
                PRIMARY KEY (session_id, enemy_id)
            )";

        public const string DungeonLootDdl = @"
            CREATE TABLE IF NOT EXISTS dungeon_loot (
                session_id TEXT,
                item_id    TEXT,
                item_type  TEXT,
                tier       TEXT,
                x          INT,
                y          INT,
                PRIMARY KEY (session_id, item_id)
            )";

        public const string EntitySnapshotsDdl = @"
            CREATE TABLE IF NOT EXISTS entity_snapshots (
                session_id   TEXT,
                entity_id    TEXT,
                entity_type  TEXT,
                version      INT,
                state_json   TEXT,
                last_updated TIMESTAMP,
                PRIMARY KEY (session_id, entity_id)
            )";

        public const string SessionMetadataDdl = @"
            CREATE TABLE IF NOT EXISTS session_metadata (
                session_id   TEXT,
                properties   MAP<TEXT, TEXT>,
                last_updated TIMESTAMP,
                PRIMARY KEY (session_id)
            )";

        public const string MasteryOffersDdl = @"
            CREATE TABLE IF NOT EXISTS mastery_offers (
                offer_id      TEXT,
                user_id       TEXT,
                item_type     TEXT,
                mastery_tier  TEXT,
                created_at    TIMESTAMP,
                options_json  TEXT,
                PRIMARY KEY (offer_id)
            )";

        public const string MasteryUnlockedDdl = @"
            CREATE TABLE IF NOT EXISTS mastery_unlocked (
                user_id      TEXT,
                item_type    TEXT,
                skill_id     TEXT,
                skill_json   TEXT,
                unlocked_at  TIMESTAMP,
                PRIMARY KEY ((user_id, item_type), skill_id)
            )";

        // ── Scylla write statements ───────────────────────────────────────────

        public const string DungeonSessionsInsert = @"
            INSERT INTO dungeon_sessions
                (session_id, execution_id, pipeline_id, seed, width, height,
                 dungeon_level, room_count, enemy_count, loot_count, created_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        public const string DungeonRoomsInsert = @"
            INSERT INTO dungeon_rooms (session_id, room_id, x, y, width, height)
            VALUES (?, ?, ?, ?, ?, ?)";

        public const string DungeonEnemiesInsert = @"
            INSERT INTO dungeon_enemies (session_id, enemy_id, archetype, x, y, level)
            VALUES (?, ?, ?, ?, ?, ?)";

        public const string DungeonLootInsert = @"
            INSERT INTO dungeon_loot (session_id, item_id, item_type, tier, x, y)
            VALUES (?, ?, ?, ?, ?, ?)";

        public const string EntitySnapshotsInsert = @"
            INSERT INTO entity_snapshots (session_id, entity_id, entity_type, version, state_json, last_updated)
            VALUES (?, ?, ?, ?, ?, ?)";

        public const string EntitySnapshotsWithTtlInsert = @"
            INSERT INTO entity_snapshots (session_id, entity_id, entity_type, version, state_json, last_updated)
            VALUES (?, ?, ?, ?, ?, ?) USING TTL ?";

        public const string SessionMetadataInsert = @"
            INSERT INTO session_metadata (session_id, properties, last_updated)
            VALUES (?, ?, ?)";

        public const string MasteryOffersInsert = @"
            INSERT INTO mastery_offers (offer_id, user_id, item_type, mastery_tier, created_at, options_json)
            VALUES (?, ?, ?, ?, ?, ?)";

        public const string MasteryUnlockedInsert = @"
            INSERT INTO mastery_unlocked (user_id, item_type, skill_id, skill_json, unlocked_at)
            VALUES (?, ?, ?, ?, ?)";

        // ── Postgres: mmodb ───────────────────────────────────────────────────

        public const string WorldSessionEventsTableDdl = @"
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
            )";

        public const string WorldSessionEventsIndexDdl = @"
            CREATE INDEX IF NOT EXISTS idx_wse_session_ts
                ON world_session_events(session_id, timestamp_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_wse_event_type
                ON world_session_events(event_type);
            CREATE INDEX IF NOT EXISTS idx_wse_frame
                ON world_session_events(session_id, frame)";

        public const string WorldSessionEventsInsert = @"
            INSERT INTO world_session_events
                (event_id, session_id, event_type, category, frame, entity_id, message, data, timestamp_utc)
            VALUES
                (@eid, @sid, @etype, @cat, @frame, @entity, @msg, @data::jsonb, @ts)
            ON CONFLICT (event_id) DO NOTHING";

        public const string AgentTaskInsert = @"
            INSERT INTO agent_tasks (id, description)
            VALUES (@id, @desc)
            RETURNING id, status, description, result, agent_log, created_at, updated_at, completed_at";

        public const string AgentTaskSelect = "id, status, description, result, agent_log, created_at, updated_at, completed_at";

        public const string AgentTasksTableDdl = @"
            CREATE TABLE IF NOT EXISTS agent_tasks (
                id           TEXT        PRIMARY KEY,
                status       TEXT        NOT NULL DEFAULT 'pending',
                description  TEXT        NOT NULL,
                result       TEXT,
                agent_log    TEXT        NOT NULL DEFAULT '',
                created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                completed_at TIMESTAMPTZ
            )";

        public const string AgentTasksIndexDdl = @"
            CREATE INDEX IF NOT EXISTS idx_agent_tasks_status ON agent_tasks (status, created_at DESC)";
    }
}
#endif