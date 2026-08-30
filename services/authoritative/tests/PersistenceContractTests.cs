using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
#if !UNITY_5_3_OR_NEWER
using Authoritative.Services;
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Tests
{
    /// <summary>
    /// Enforces schema &lt;-&gt; model lockstep for every persistence surface on the
    /// authoritative backend (Scylla mmo_world, Postgres mmodb, Redis, and the
    /// canonical db/scylla/mmo_world.cql + db/migrations/ files), plus the
    /// shared tag vocabulary alignment with the Unity client catalog.
    ///
    /// Data is pure string/reflection inspection: no live databases, no network.
    /// </summary>
    public class PersistenceContractTests
    {
        // ── Canonical column lists (the schema contract) ──────────────────────

        private static readonly string[] DungeonSessionsColumns =
        {
            "session_id", "execution_id", "pipeline_id", "seed", "width", "height",
            "dungeon_level", "room_count", "enemy_count", "loot_count", "created_at"
        };

        private static readonly string[] DungeonRoomsColumns =
        {
            "session_id", "room_id", "x", "y", "width", "height"
        };

        private static readonly string[] DungeonEnemiesColumns =
        {
            "session_id", "enemy_id", "archetype", "x", "y", "level"
        };

        private static readonly string[] DungeonLootColumns =
        {
            "session_id", "item_id", "item_type", "tier", "x", "y"
        };

        private static readonly string[] EntitySnapshotsColumns =
        {
            "session_id", "entity_id", "entity_type", "version", "state_json", "last_updated"
        };

        private static readonly string[] SessionMetadataColumns =
        {
            "session_id", "properties", "last_updated"
        };

        private static readonly string[] MasteryOffersColumns =
        {
            "offer_id", "user_id", "item_type", "mastery_tier", "created_at", "options_json"
        };

        private static readonly string[] MasteryUnlockedColumns =
        {
            "user_id", "item_type", "skill_id", "skill_json", "unlocked_at"
        };

        private static readonly string[] WorldSessionEventsColumns =
        {
            "event_id", "session_id", "event_type", "category", "frame", "entity_id",
            "message", "data", "timestamp_utc"
        };

        private static readonly string[] AgentTasksColumns =
        {
            "id", "status", "description", "result", "agent_log",
            "created_at", "updated_at", "completed_at"
        };

        // Unity client-truth tags (mirrors NpcPersonalityGenerator bias cases).
        private static readonly string[] UnityNpcBiasArchetypes =
        {
            "goblin", "skeleton", "zombie", "undead", "cultist", "fanatic",
            "bandit", "mercenary", "wolf", "beast", "mage", "wizard",
            "sorcerer", "guard", "soldier"
        };

        // Unity GameSession.cs emitters — client truth for world_session_events.
        private const string UnitySystemExecuteEventType = "system.execute";
        private const string UnityEntityStateSnapshotEventType = "entity.state.snapshot";
        private const string UnityEntityStateSnapshotSummaryEventType = "entity.state.snapshot.summary";

        // ── Scylla: schema ↔ INSERT ↔ row model ───────────────────────────────

        [FactAttribute]
        public void DungeonSessions_DdlMatchesInsertAndRowModel()
        {
            var ddl = DdlColumns("dungeon_sessions", PersistenceSchemaText.DungeonSessionsDdl);
            var insert = InsertColumns(PersistenceSchemaText.DungeonSessionsInsert);

            AssertEqualSets(DungeonSessionsColumns, ddl, "dungeon_sessions DDL");
            AssertEqualSets(DungeonSessionsColumns, insert, "dungeon_sessions INSERT");

            var model = ModelColumns(typeof(WorldSessionRow));
            AssertEqualSets(DungeonSessionsColumns, model, "WorldSessionRow model");
        }

        [FactAttribute]
        public void DungeonRooms_DdlMatchesInsertAndRowModel()
        {
            AssertColumnContractWithShard("dungeon_rooms", PersistenceSchemaText.DungeonRoomsDdl,
                PersistenceSchemaText.DungeonRoomsInsert, typeof(WorldRoomRow), DungeonRoomsColumns);
        }

        [FactAttribute]
        public void DungeonEnemies_DdlMatchesInsertAndRowModel()
        {
            AssertColumnContractWithShard("dungeon_enemies", PersistenceSchemaText.DungeonEnemiesDdl,
                PersistenceSchemaText.DungeonEnemiesInsert, typeof(WorldEnemyRow), DungeonEnemiesColumns);
        }

        [FactAttribute]
        public void DungeonLoot_DdlMatchesInsertAndRowModel()
        {
            AssertColumnContractWithShard("dungeon_loot", PersistenceSchemaText.DungeonLootDdl,
                PersistenceSchemaText.DungeonLootInsert, typeof(WorldLootRow), DungeonLootColumns);
        }

        [FactAttribute]
        public void EntitySnapshots_DdlMatchesBothInsertVariants()
        {
            var ddl = DdlColumns("entity_snapshots", PersistenceSchemaText.EntitySnapshotsDdl);
            AssertEqualSets(EntitySnapshotsColumns, ddl, "entity_snapshots DDL");
            AssertEqualSets(EntitySnapshotsColumns, InsertColumns(PersistenceSchemaText.EntitySnapshotsInsert), "entity_snapshots INSERT");
            AssertEqualSets(EntitySnapshotsColumns, InsertColumns(PersistenceSchemaText.EntitySnapshotsWithTtlInsert), "entity_snapshots INSERT (TTL)");
        }

        [FactAttribute]
        public void SessionMetadata_DdlMatchesInsert()
        {
            var ddl = DdlColumns("session_metadata", PersistenceSchemaText.SessionMetadataDdl);
            AssertEqualSets(SessionMetadataColumns, ddl, "session_metadata DDL");
            AssertEqualSets(SessionMetadataColumns, InsertColumns(PersistenceSchemaText.SessionMetadataInsert), "session_metadata INSERT");
        }

        [FactAttribute]
        public void MasteryOffers_DdlMatchesInsertAndOfferModel()
        {
            var ddl = DdlColumns("mastery_offers", PersistenceSchemaText.MasteryOffersDdl);
            var insert = InsertColumns(PersistenceSchemaText.MasteryOffersInsert);

            AssertEqualSets(MasteryOffersColumns, ddl, "mastery_offers DDL");
            AssertEqualSets(MasteryOffersColumns, insert, "mastery_offers INSERT");

            var renameMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Options"] = "options_json",
                ["CreatedAtUtc"] = "created_at"
            };
            var model = ModelColumns(typeof(MasteryOffer), renameMap);
            AssertEqualSets(MasteryOffersColumns, model, "MasteryOffer model");
        }

        [FactAttribute]
        public void MasteryUnlocked_DdlMatchesInsert()
        {
            var ddl = DdlColumns("mastery_unlocked", PersistenceSchemaText.MasteryUnlockedDdl);
            AssertEqualSets(MasteryUnlockedColumns, ddl, "mastery_unlocked DDL");
            AssertEqualSets(MasteryUnlockedColumns, InsertColumns(PersistenceSchemaText.MasteryUnlockedInsert), "mastery_unlocked INSERT");
        }

        // ── Canonical cql ↔ code schema ───────────────────────────────────────

        [FactAttribute]
        public void MmoWorldCql_IsInSyncWithCodeSchema()
        {
            var cql = File.ReadAllText(Path.Combine(RepoRoot, "db", "scylla", "mmo_world.cql"));

            AssertEqualSets(DdlColumns("dungeon_sessions", cql), DungeonSessionsColumns, "cql dungeon_sessions");
            AssertEqualSets(DdlColumns("dungeon_rooms", cql), DungeonRoomsColumns, "cql dungeon_rooms");
            AssertEqualSets(DdlColumns("dungeon_enemies", cql), DungeonEnemiesColumns, "cql dungeon_enemies");
            AssertEqualSets(DdlColumns("dungeon_loot", cql), DungeonLootColumns, "cql dungeon_loot");
            AssertEqualSets(DdlColumns("entity_snapshots", cql), EntitySnapshotsColumns, "cql entity_snapshots");
            AssertEqualSets(DdlColumns("session_metadata", cql), SessionMetadataColumns, "cql session_metadata");
            AssertEqualSets(DdlColumns("mastery_offers", cql), MasteryOffersColumns, "cql mastery_offers");
            AssertEqualSets(DdlColumns("mastery_unlocked", cql), MasteryUnlockedColumns, "cql mastery_unlocked");
        }

        // ── Postgres: schema ↔ migration ↔ INSERT ↔ model ────────────────────

        [FactAttribute]
        public void WorldSessionEvents_DdlMatchesMigrationInsertAndModel()
        {
            var ddl = DdlColumns("world_session_events", PersistenceSchemaText.WorldSessionEventsTableDdl);
            AssertEqualSets(WorldSessionEventsColumns, ddl, "world_session_events DDL");
            AssertEqualSets(WorldSessionEventsColumns, InsertColumns(PersistenceSchemaText.WorldSessionEventsInsert), "world_session_events INSERT");
            AssertEqualSets(WorldSessionEventsColumns, ModelColumns(typeof(WorldSessionEvent)), "WorldSessionEvent model");

            var migrationDdl = DdlColumns("world_session_events",
                File.ReadAllText(Path.Combine(RepoRoot, "db", "migrations", "0001_create_world_session_events.sql")));
            AssertEqualSets(WorldSessionEventsColumns, migrationDdl, "migration 0001");
        }

        [FactAttribute]
        public void AgentTasks_DdlMatchesMigrationSelectAndModel()
        {
            var ddl = DdlColumns("agent_tasks", PersistenceSchemaText.AgentTasksTableDdl);
            AssertEqualSets(AgentTasksColumns, ddl, "agent_tasks DDL");

            AssertEqualSets(new[] { "id", "description" }, InsertColumns(PersistenceSchemaText.AgentTaskInsert), "agent_tasks INSERT");
            AssertEqualSets(AgentTasksColumns, ReturnColumns(PersistenceSchemaText.AgentTaskInsert), "agent_tasks INSERT RETURNING");
            AssertEqualSets(AgentTasksColumns, SplitList(PersistenceSchemaText.AgentTaskSelect), "agent_tasks SELECT");
            AssertEqualSets(AgentTasksColumns, ModelColumns(typeof(AgentTask)), "AgentTask model");

            var migrationDdl = DdlColumns("agent_tasks",
                File.ReadAllText(Path.Combine(RepoRoot, "db", "migrations", "0002_create_agent_tasks.sql")));
            AssertEqualSets(AgentTasksColumns, migrationDdl, "migration 0002");
        }

        [FactAttribute]
        public void CharacterPartsMigration_IsUnusedByCodeButSupported()
        {
            var migrationDdl = DdlColumns("character_parts",
                File.ReadAllText(Path.Combine(RepoRoot, "db", "migrations", "0003_create_character_parts.sql")));
            Assert.NotEmpty(migrationDdl);
            Assert.Contains("part_id", migrationDdl, StringComparer.Ordinal);
            Assert.Contains("asset_path", migrationDdl, StringComparer.Ordinal);
            Assert.Contains("meta", migrationDdl, StringComparer.Ordinal);
        }

        // ── Shared tag vocabulary vs Unity client truth ───────────────────────

        [FactAttribute]
        public void EnemyArchetypes_AreSubsetOfUnityNpcBiasCatalog()
        {
            var unity = new HashSet<string>(UnityNpcBiasArchetypes, StringComparer.OrdinalIgnoreCase);
            foreach (var archetype in PersistenceTagCatalog.EnemyArchetypes)
                Assert.True(unity.Contains(archetype),
                    $"Server archetype '{archetype}' is not covered by Unity's NpcPersonalityGenerator bias cases.");
        }

        [FactAttribute]
        public void LootItemTypes_AreCanonicalAndWellFormed()
        {
            var all = PersistenceTagCatalog.LootItemTypes
                .Append(PersistenceTagCatalog.FallbackLootItemType)
                .Concat(PersistenceTagCatalog.MasteryExtraItemTypes)
                .ToArray();

            Assert.All(all, value =>
            {
                Assert.False(string.IsNullOrWhiteSpace(value));
                Assert.Equal(value, value.ToLowerInvariant());
                Assert.DoesNotContain(' ', value);
                Assert.DoesNotContain('.', value);
            });

            Assert.Equal(6, PersistenceTagCatalog.LootItemTypes.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(PersistenceTagCatalog.FallbackLootItemType, PersistenceTagCatalog.LootItemTypes, StringComparer.Ordinal);
            Assert.DoesNotContain(PersistenceTagCatalog.FallbackLootItemType, PersistenceTagCatalog.MasteryExtraItemTypes, StringComparer.Ordinal);
        }

        [FactAttribute]
        public void WorldEventTypes_MatchUnityEmitters()
        {
            Assert.Equal(UnityEntityStateSnapshotEventType, PersistenceTagCatalog.EntityStateSnapshotEventType);
            Assert.StartsWith(PersistenceTagCatalog.SystemEventTypePrefix, UnitySystemExecuteEventType);
            Assert.StartsWith(PersistenceTagCatalog.EntityStateSnapshotEventType, UnityEntityStateSnapshotSummaryEventType);
            Assert.NotEqual(PersistenceTagCatalog.EntityStateSnapshotEventType, UnityEntityStateSnapshotSummaryEventType);
        }

        // ── Parsing helpers ───────────────────────────────────────────────────

        private static void AssertColumnContractWithShard(
            string table, string ddlText, string insertText, Type modelType, string[] expected)
        {
            var ddl = DdlColumns(table, ddlText);
            var insert = InsertColumns(insertText);

            AssertEqualSets(expected, ddl, $"{table} DDL");
            AssertEqualSets(expected, insert, $"{table} INSERT");

            var model = ModelColumns(modelType).Append("session_id").ToArray();
            AssertEqualSets(expected, model, $"{modelType.Name} model (+ session_id shard)");
        }

        private static void AssertEqualSets(string[] expected, string[] actual, string label)
        {
            Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
                         actual.OrderBy(x => x, StringComparer.Ordinal));
        }

        private static string[] ModelColumns(Type modelType, IReadOnlyDictionary<string, string>? renameMap = null)
        {
            var props = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.NotEmpty(props);
            return props
                .Select(p => renameMap != null && renameMap.TryGetValue(p.Name, out var renamed)
                    ? renamed
                    : ToSnakeCase(p.Name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string ToSnakeCase(string pascal)
        {
            var sb = new StringBuilder(pascal.Length + 4);
            for (var i = 0; i < pascal.Length; i++)
            {
                var c = pascal[i];
                if (i > 0 && char.IsUpper(c))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static string[] DdlColumns(string table, string ddlText)
        {
            var body = ExtractTableBody(table, ddlText);
            var columns = new List<string>();
            foreach (var part in SplitTopLevel(body))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    continue;
                var tokens = trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                columns.Add(tokens[0].ToLowerInvariant());
            }
            return columns.ToArray();
        }

        private static string ExtractTableBody(string table, string ddlText)
        {
            var marker = "CREATE TABLE IF NOT EXISTS " + table;
            var start = ddlText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            Assert.True(start >= 0, $"No CREATE TABLE IF NOT EXISTS {table} found.");

            var open = ddlText.IndexOf('(', start + marker.Length);
            Assert.True(open >= 0, $"No column list opening '(' found for {table}.");

            var depth = 0;
            var body = new StringBuilder();
            for (var i = open; i < ddlText.Length; i++)
            {
                var c = ddlText[i];
                switch (c)
                {
                    case '(':
                        depth++;
                        if (depth > 1) body.Append(c);
                        break;
                    case ')':
                        depth--;
                        if (depth == 0) return body.ToString();
                        if (depth >= 1) body.Append(c);
                        break;
                    default:
                        body.Append(c);
                        break;
                }
            }

            Assert.True(false, $"Unbalanced parentheses while parsing {table}.");
            return string.Empty;
        }

        private static List<string> SplitTopLevel(string text)
        {
            var parts = new List<string>();
            var depth = 0;
            var angleDepth = 0;
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == '<') angleDepth++;
                else if (c == '>') angleDepth--;
                else if (c == ',' && depth == 0 && angleDepth == 0)
                {
                    parts.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(text.Substring(start));
            return parts;
        }

        private static string[] InsertColumns(string insertText)
        {
            var match = Regex.Match(insertText, @"INSERT INTO \w+\s*\((?<cols>[^)]*)\)", RegexOptions.IgnoreCase);
            Assert.True(match.Success, "No INSERT column list parsed.");
            return SplitList(match.Groups["cols"].Value);
        }

        private static string[] ReturnColumns(string insertText)
        {
            var match = Regex.Match(insertText, @"RETURNING\s+(?<cols>[^)]+)", RegexOptions.IgnoreCase);
            Assert.True(match.Success, "No RETURNING column list parsed.");
            return SplitList(match.Groups["cols"].Value);
        }

        private static string[] SplitList(string value)
        {
            return value
                .Split(',')
                .Select(x => x.Trim().ToLowerInvariant())
                .Select(x => x.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0])
                .Where(x => x.Length > 0)
                .ToArray();
        }

        private static string RepoRoot
        {
            get
            {
                // The test host's working directory is the test bin output dir
                // (repo/Assets/DunGenMMORPGEngine/Library/... for this workspace),
                // so walk ancestors looking for the repo root that owns
                // db/scylla/mmo_world.cql either at itself or under Assets/DunGenMMORPGEngine.
                var dir = new DirectoryInfo(Environment.CurrentDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "db", "scylla", "mmo_world.cql")))
                        return dir.FullName;

                    var nestedCandidate = Path.Combine(dir.FullName, "Assets", "DunGenMMORPGEngine");
                    if (File.Exists(Path.Combine(nestedCandidate, "db", "scylla", "mmo_world.cql")))
                        return nestedCandidate;

                    dir = dir.Parent;
                }
                throw new InvalidOperationException(
                    "Could not locate repo root containing 'db/scylla/mmo_world.cql' from " + Environment.CurrentDirectory);
            }
        }
    }
}
#endif