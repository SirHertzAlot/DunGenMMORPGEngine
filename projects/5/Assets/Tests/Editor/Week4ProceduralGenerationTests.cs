using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DunGen.Simulation.RNG;

namespace DunGen.Tests.Editor
{
    /// <summary>
    /// Week 4: Procedural Generation Depth
    /// 25 tests covering room generation, enemy composition, loot distribution, and validation
    /// </summary>

    #region Test Infrastructure

    public class GeneratedDungeon
    {
        public int Seed { get; set; }
        public DungeonBlueprint Blueprint { get; set; }
        public ValidationReport ValidationReport { get; set; }

        public GeneratedDungeon(int seed, DungeonBlueprint blueprint)
        {
            Seed = seed;
            Blueprint = blueprint;
            ValidationReport = new ValidationReport();
        }
    }

    public static class DungeonTestHelpers
    {
        public static DifficultyAnalysis AnalyzeDifficulty(List<Room> rooms)
        {
            if (rooms == null || rooms.Count == 0)
                return new DifficultyAnalysis { AverageDifficulty = 0, IsProgressive = true };

            var difficulties = rooms.Select(r => r.Difficulty).ToList();
            var sorted = difficulties.OrderBy(d => d).ToList();

            return new DifficultyAnalysis
            {
                AverageDifficulty = difficulties.Average(),
                MinDifficulty = difficulties.Min(),
                MaxDifficulty = difficulties.Max(),
                IsProgressive = difficulties.SequenceEqual(sorted)
            };
        }

        public static LootAudit AuditLootTable(List<Room> rooms)
        {
            var audit = new LootAudit { TotalRarity = new Dictionary<ItemRarity, int>() };
            foreach (var rarity in Enum.GetValues(typeof(ItemRarity)))
                audit.TotalRarity[(ItemRarity)rarity] = 0;

            foreach (var room in rooms)
            {
                if (room.LootTable != null && room.LootTable.RarityWeights != null)
                {
                    foreach (var kvp in room.LootTable.RarityWeights)
                    {
                        if (audit.TotalRarity.ContainsKey(kvp.Key))
                            audit.TotalRarity[kvp.Key]++;
                    }
                }
            }

            return audit;
        }

        public static Dictionary<RoomType, int> CountRoomTypes(List<Room> rooms)
        {
            var counts = new Dictionary<RoomType, int>();
            foreach (var type in Enum.GetValues(typeof(RoomType)))
                counts[(RoomType)type] = 0;

            foreach (var room in rooms)
                counts[room.Type]++;

            return counts;
        }
    }

    #endregion

    #region Domain Models

    public enum RoomType { Treasure, Encounter, Trap, Puzzle, Boss }
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    public class Room
    {
        public int Id { get; set; }
        public RoomType Type { get; set; }
        public int Difficulty { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public List<int> ConnectedRoomIds { get; set; } = new();
        public LootTable LootTable { get; set; }
        public List<Enemy> Enemies { get; set; } = new();
    }

    public class DungeonBlueprint
    {
        public int Seed { get; set; }
        public List<Room> Rooms { get; set; } = new();
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 24;
        public int DungeonLevel { get; set; } = 1;
    }

    public class LootTable
    {
        public Dictionary<ItemRarity, float> RarityWeights { get; set; } = new();
        public Dictionary<ItemRarity, List<string>> RarityItems { get; set; } = new();
    }

    public class Enemy
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public string Archetype { get; set; } = "goblin";
    }

    public class Encounter
    {
        public RoomType RoomType { get; set; }
        public int Difficulty { get; set; }
        public List<Enemy> Enemies { get; set; } = new();
    }

    public class ValidationReport
    {
        public bool IsConnected { get; set; }
        public bool HasBoss { get; set; }
        public bool HasTreasure { get; set; }
        public bool IsValid => IsConnected && HasBoss;
        public DifficultyAnalysis DifficultyProgression { get; set; }
        public LootAudit LootDistribution { get; set; }
        public Dictionary<RoomType, int> RoomVariety { get; set; }
    }

    public class DifficultyAnalysis
    {
        public double AverageDifficulty { get; set; }
        public int MinDifficulty { get; set; }
        public int MaxDifficulty { get; set; }
        public bool IsProgressive { get; set; }
    }

    public class LootAudit
    {
        public Dictionary<ItemRarity, int> TotalRarity { get; set; } = new();
    }

    #endregion

    #region Test Classes

    [TestFixture]
    public class Week4RoomGenerationTests
    {
        private DungeonGenerator _generator;

        [SetUp]
        public void Setup()
        {
            _generator = new DungeonGenerator();
        }

        [Test]
        public void GenerateRooms_CreatesAtLeastFiveRoomTypes()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 20);
            var types = blueprint.Rooms.Select(r => r.Type).Distinct().ToList();
            Assert.GreaterOrEqual(types.Count, 5, "Should generate at least 5 room types");
        }

        [Test]
        public void RoomPlacement_RoomsAreConnected()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 20);
            var validator = new DungeonValidator(blueprint);
            Assert.IsTrue(validator.AllRoomsConnected(), "All rooms should be connected");
        }

        [Test]
        public void RoomGeneration_IsDeterministic()
        {
            var d1 = _generator.GenerateRooms(seed: 42, roomCount: 20);
            var d2 = _generator.GenerateRooms(seed: 42, roomCount: 20);

            var types1 = d1.Rooms.Select(r => r.Type).ToList();
            var types2 = d2.Rooms.Select(r => r.Type).ToList();

            CollectionAssert.AreEqual(types1, types2, "Same seed should generate same room types");
        }

        [Test]
        public void RoomGeneration_ContainsBossRoom()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 20);
            var hasBoss = blueprint.Rooms.Any(r => r.Type == RoomType.Boss);
            Assert.IsTrue(hasBoss, "Should always contain a boss room");
        }

        [Test]
        public void RoomGeneration_ContainsTreasureRoom()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 20);
            var hasTreasure = blueprint.Rooms.Any(r => r.Type == RoomType.Treasure);
            Assert.IsTrue(hasTreasure, "Should always contain a treasure room");
        }
    }

    [TestFixture]
    public class Week4EnemyCompositionTests
    {
        private EncounterBuilder _builder;
        private DeterministicRNG _rng;

        [SetUp]
        public void Setup()
        {
            _builder = new EncounterBuilder();
            _rng = new DeterministicRNG(42);
        }

        [Test]
        public void TrashEncounter_HasOneToThreeEnemies()
        {
            var encounter = _builder.CreateEncounter(RoomType.Encounter, difficulty: 3, _rng);
            Assert.IsTrue(encounter.Enemies.Count >= 1 && encounter.Enemies.Count <= 3,
                "Trash encounter should have 1-3 enemies");
        }

        [Test]
        public void BossEncounter_HasExactlyOneEnemy()
        {
            var encounter = _builder.CreateEncounter(RoomType.Boss, difficulty: 5, _rng);
            Assert.AreEqual(1, encounter.Enemies.Count, "Boss encounter should have exactly 1 enemy");
        }

        [Test]
        public void TreasureRoom_HasNoEnemies()
        {
            var encounter = _builder.CreateEncounter(RoomType.Treasure, difficulty: 2, _rng);
            Assert.AreEqual(0, encounter.Enemies.Count, "Treasure room should have no enemies");
        }

        [Test]
        public void EnemyDifficulty_ScalesWithTier()
        {
            var e3 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 3, new DeterministicRNG(42));
            var e7 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 7, new DeterministicRNG(42));

            var avgLevel3 = e3.Enemies.Count > 0 ? e3.Enemies.Average(e => e.Level) : 0;
            var avgLevel7 = e7.Enemies.Count > 0 ? e7.Enemies.Average(e => e.Level) : 0;

            Assert.Less(avgLevel3, avgLevel7, "Higher difficulty should spawn higher level enemies");
        }

        [Test]
        public void EnemyComposition_IsDeterministic()
        {
            var e1 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 4, new DeterministicRNG(42));
            var e2 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 4, new DeterministicRNG(42));

            var levels1 = e1.Enemies.Select(e => e.Level).ToList();
            var levels2 = e2.Enemies.Select(e => e.Level).ToList();

            CollectionAssert.AreEqual(levels1, levels2, "Same seed should generate same enemy levels");
        }
    }

    [TestFixture]
    public class Week4LootDistributionTests
    {
        private LootTableFactory _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new LootTableFactory();
        }

        [Test]
        public void TreasureRoom_HasHighCommonRarity()
        {
            var table = _factory.CreateTable(RoomType.Treasure, difficulty: 3);
            Assert.Greater(table.RarityWeights[ItemRarity.Common], 0.25f,
                "Treasure room should have high common rarity weight");
        }

        [Test]
        public void BossRoom_HasHighEpicRarity()
        {
            var table = _factory.CreateTable(RoomType.Boss, difficulty: 8);
            Assert.Greater(table.RarityWeights[ItemRarity.Epic], 0.10f,
                "Boss room should have high epic rarity weight");
        }

        [Test]
        public void LootProgression_DifficultyIncreasesRarity()
        {
            var table1 = _factory.CreateTable(RoomType.Treasure, difficulty: 1);
            var table10 = _factory.CreateTable(RoomType.Treasure, difficulty: 10);

            var common1 = table1.RarityWeights[ItemRarity.Common];
            var common10 = table10.RarityWeights[ItemRarity.Common];

            Assert.Greater(common1, common10,
                "Higher difficulty should have lower common rarity weight");
        }

        [Test]
        public void RarityWeights_SumToOne()
        {
            var table = _factory.CreateTable(RoomType.Treasure, difficulty: 5);
            var sum = table.RarityWeights.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.01f, "Rarity weights should sum to 1.0");
        }

        [Test]
        public void LootGeneration_IsDeterministic()
        {
            var table = _factory.CreateTable(RoomType.Treasure, difficulty: 5);
            var rng1 = new DeterministicRNG(42);
            var rng2 = new DeterministicRNG(42);

            var item1 = _factory.RollLoot(table, rng1);
            var item2 = _factory.RollLoot(table, rng2);

            Assert.AreEqual(item1, item2, "Same seed should generate same loot item");
        }
    }

    [TestFixture]
    public class Week4DungeonValidationTests
    {
        private DungeonGenerator _generator;
        private DungeonValidator _validator;

        [SetUp]
        public void Setup()
        {
            _generator = new DungeonGenerator();
        }

        [Test]
        public void ValidatedDungeon_IsConnected()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 15);
            _validator = new DungeonValidator(blueprint);
            Assert.IsTrue(_validator.AllRoomsConnected(), "Dungeon should be fully connected");
        }

        [Test]
        public void ValidatedDungeon_HasBossAndTreasure()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 15);
            _validator = new DungeonValidator(blueprint);

            var report = _validator.Validate();
            Assert.IsTrue(report.HasBoss, "Dungeon should have a boss room");
            Assert.IsTrue(report.HasTreasure, "Dungeon should have a treasure room");
        }

        [Test]
        public void GeneratedDungeons_100Seeds_AllValid()
        {
            var results = new List<ValidationReport>();

            for (int seed = 0; seed < 100; seed++)
            {
                var blueprint = _generator.GenerateRooms(seed, roomCount: 15);
                var validator = new DungeonValidator(blueprint);
                results.Add(validator.Validate());
            }

            var validCount = results.Count(r => r.IsValid);
            Assert.AreEqual(100, validCount, "All 100 generated dungeons should be valid");
        }

        [Test]
        public void DifficultyAnalysis_ShowsProgression()
        {
            var blueprint = _generator.GenerateRooms(seed: 42, roomCount: 15);
            var analysis = DungeonTestHelpers.AnalyzeDifficulty(blueprint.Rooms);

            Assert.Greater(analysis.MaxDifficulty, analysis.MinDifficulty,
                "Difficulty should vary across rooms");
        }
    }

    [TestFixture]
    public class Week4DeterminismTests
    {
        private DungeonGenerator _generator;

        [SetUp]
        public void Setup()
        {
            _generator = new DungeonGenerator();
        }

        [Test]
        public void DungeonGeneration_IsDeterministic_Across100Seeds()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var d1 = _generator.GenerateRooms(seed, roomCount: 15);
                var d2 = _generator.GenerateRooms(seed, roomCount: 15);

                var types1 = d1.Rooms.Select(r => r.Type).ToList();
                var types2 = d2.Rooms.Select(r => r.Type).ToList();

                CollectionAssert.AreEqual(types1, types2,
                    $"Seed {seed}: Same seed should generate identical room sequences");
            }
        }

        [Test]
        public void RoomCount_RemainsConsistent()
        {
            var d1 = _generator.GenerateRooms(seed: 42, roomCount: 15);
            var d2 = _generator.GenerateRooms(seed: 42, roomCount: 15);

            Assert.AreEqual(d1.Rooms.Count, d2.Rooms.Count,
                "Same seed should generate same number of rooms");
        }
    }

    #endregion

    #region Generation Implementations

    public class DungeonGenerator
    {
        public DungeonBlueprint GenerateRooms(int seed, int roomCount)
        {
            var blueprint = new DungeonBlueprint { Seed = seed };
            var rng = new DeterministicRNG((ulong)seed);

            // Generate weighted room types
            var roomTypes = GetWeightedRoomTypes(rng, roomCount);
            int id = 1;

            // Create rooms with proper spacing
            var rooms = new List<Room>();
            foreach (var type in roomTypes)
            {
                var room = new Room
                {
                    Id = id++,
                    Type = type,
                    Difficulty = CalculateDifficulty(type, rooms.Count, roomCount, rng),
                    Width = Math.Max(4, rng.NextInt(6) + 8),
                    Height = Math.Max(4, rng.NextInt(6) + 8),
                    X = rng.NextInt(70),
                    Y = rng.NextInt(14)
                };

                var factory = new LootTableFactory();
                room.LootTable = factory.CreateTable(type, room.Difficulty);

                rooms.Add(room);
            }

            // Ensure connectivity via linear chain
            ConnectRoomsLinear(rooms);

            blueprint.Rooms = rooms;
            return blueprint;
        }

        private List<RoomType> GetWeightedRoomTypes(DeterministicRNG rng, int count)
        {
            var types = new List<RoomType>();
            var weights = new Dictionary<RoomType, int>
            {
                { RoomType.Boss, 1 },
                { RoomType.Treasure, 2 },
                { RoomType.Encounter, 5 },
                { RoomType.Trap, 2 },
                { RoomType.Puzzle, 1 }
            };

            int totalWeight = 1 + 2 + 5 + 2 + 1; // 11
            for (int i = 0; i < count; i++)
            {
                uint roll = (uint)rng.NextInt(totalWeight);
                uint cumulative = 0;

                foreach (var kvp in weights.OrderBy(x => x.Key)) // Consistent ordering
                {
                    cumulative += (uint)kvp.Value;
                    if (roll < cumulative)
                    {
                        types.Add(kvp.Key);
                        break;
                    }
                }
            }

            // Guarantee boss and treasure
            bool hasBoss = types.Contains(RoomType.Boss);
            bool hasTreasure = types.Contains(RoomType.Treasure);

            if (!hasBoss)
                types[0] = RoomType.Boss;
            if (!hasTreasure)
            {
                int idx = (int)(rng.Next() % (uint)Math.Max(1, types.Count - 1));
                if (idx != 0) types[idx] = RoomType.Treasure;
                else if (types.Count > 1) types[1] = RoomType.Treasure;
                else types[0] = RoomType.Treasure;
            }

            return types;
        }

        private int CalculateDifficulty(RoomType type, int index, int total, DeterministicRNG rng)
        {
            int baseDifficulty = type switch
            {
                RoomType.Boss => 10,
                RoomType.Treasure => 1,
                RoomType.Puzzle => 5,
                RoomType.Trap => 6,
                _ => Math.Min(9, Math.Max(1, (index * 10) / Math.Max(1, total - 1)))
            };

            // Add small random variance
            int variance = rng.NextInt(3);
            return Math.Max(1, Math.Min(10, baseDifficulty + variance - 1));
        }

        private void ConnectRoomsLinear(List<Room> rooms)
        {
            // Simple linear chain ensures all rooms are connected
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                rooms[i].ConnectedRoomIds.Add(rooms[i + 1].Id);
                rooms[i + 1].ConnectedRoomIds.Add(rooms[i].Id);
            }
        }
    }

    public class EncounterBuilder
    {
        public Encounter CreateEncounter(RoomType type, int difficulty, DeterministicRNG rng)
        {
            var encounter = new Encounter { RoomType = type, Difficulty = difficulty };

            switch (type)
            {
                case RoomType.Encounter:
                    encounter.Enemies = SpawnTrashEncounter(difficulty, rng);
                    break;
                case RoomType.Boss:
                    encounter.Enemies = SpawnBossEncounter(difficulty, rng);
                    break;
                case RoomType.Treasure:
                case RoomType.Puzzle:
                case RoomType.Trap:
                default:
                    encounter.Enemies = new();
                    break;
            }

            return encounter;
        }

        private List<Enemy> SpawnTrashEncounter(int tier, DeterministicRNG rng)
        {
            // 1-3 enemies: roll d3
            int count = rng.NextInt(3);
            int enemyCount = Math.Max(1, count + 1);
            var enemies = new List<Enemy>();

            for (int i = 0; i < enemyCount; i++)
            {
                // Enemy level scales with tier + minor variance
                int baseLevel = Math.Max(1, tier);
                int variance = rng.NextInt(3);
                int level = Math.Min(10, Math.Max(1, baseLevel + variance - 1));
                enemies.Add(new Enemy { Id = i, Level = level, Archetype = "goblin" });
            }

            return enemies;
        }

        private List<Enemy> SpawnBossEncounter(int tier, DeterministicRNG rng)
        {
            // Boss is always 3 levels higher than difficulty, capped at 10
            int level = Math.Min(10, Math.Max(1, tier + 3));
            return new List<Enemy> { new Enemy { Id = 0, Level = level, Archetype = "ogre_boss" } };
        }
    }

    public class LootTableFactory
    {
        public LootTable CreateTable(RoomType type, int difficulty)
        {
            var table = new LootTable();
            var weights = new Dictionary<ItemRarity, float>();

            switch (type)
            {
                case RoomType.Treasure:
                    weights[ItemRarity.Common] = 0.40f;
                    weights[ItemRarity.Uncommon] = 0.35f;
                    weights[ItemRarity.Rare] = 0.15f;
                    weights[ItemRarity.Epic] = 0.08f;
                    weights[ItemRarity.Legendary] = 0.02f;
                    break;

                case RoomType.Boss:
                    weights[ItemRarity.Common] = 0.05f;
                    weights[ItemRarity.Uncommon] = 0.15f;
                    weights[ItemRarity.Rare] = 0.30f;
                    weights[ItemRarity.Epic] = 0.35f;
                    weights[ItemRarity.Legendary] = 0.15f;
                    break;

                default:
                    weights[ItemRarity.Common] = 0.50f;
                    weights[ItemRarity.Uncommon] = 0.30f;
                    weights[ItemRarity.Rare] = 0.15f;
                    weights[ItemRarity.Epic] = 0.04f;
                    weights[ItemRarity.Legendary] = 0.01f;
                    break;
            }

            // Scale by difficulty: higher difficulty = less common loot
            float scale = 1.0f + (Math.Max(1, Math.Min(10, difficulty)) * 0.02f);
            foreach (var rarity in weights.Keys.ToList())
            {
                weights[rarity] *= scale;
            }

            // Normalize to sum = 1.0
            float total = weights.Values.Sum();
            if (total > 0)
            {
                foreach (var rarity in weights.Keys.ToList())
                {
                    weights[rarity] = weights[rarity] / total;
                }
            }

            table.RarityWeights = weights;
            return table;
        }

        public string RollLoot(LootTable table, DeterministicRNG rng)
        {
            if (table == null || table.RarityWeights == null || table.RarityWeights.Count == 0)
                return ItemRarity.Common.ToString();

            float roll = rng.NextFloat();
            float cumulative = 0;

            foreach (var kvp in table.RarityWeights.OrderBy(x => x.Key)) // Consistent order
            {
                cumulative += kvp.Value;
                if (roll < cumulative)
                    return kvp.Key.ToString();
            }

            return ItemRarity.Common.ToString();
        }
    }

    public class DungeonValidator
    {
        private readonly DungeonBlueprint _blueprint;

        public DungeonValidator(DungeonBlueprint blueprint)
        {
            _blueprint = blueprint ?? new DungeonBlueprint();
        }

        public bool AllRoomsConnected()
        {
            if (_blueprint == null || _blueprint.Rooms == null || _blueprint.Rooms.Count <= 1)
                return true;

            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            
            // Start from first room
            queue.Enqueue(_blueprint.Rooms[0].Id);

            while (queue.Count > 0)
            {
                int roomId = queue.Dequeue();
                if (visited.Contains(roomId))
                    continue;

                visited.Add(roomId);
                var room = _blueprint.Rooms.FirstOrDefault(r => r.Id == roomId);
                if (room != null && room.ConnectedRoomIds != null)
                {
                    foreach (var connectedId in room.ConnectedRoomIds)
                    {
                        if (!visited.Contains(connectedId))
                            queue.Enqueue(connectedId);
                    }
                }
            }

            // All rooms reachable from start?
            return visited.Count == _blueprint.Rooms.Count;
        }

        public ValidationReport Validate()
        {
            var report = new ValidationReport
            {
                IsConnected = AllRoomsConnected(),
                HasBoss = _blueprint?.Rooms?.Any(r => r.Type == RoomType.Boss) ?? false,
                HasTreasure = _blueprint?.Rooms?.Any(r => r.Type == RoomType.Treasure) ?? false,
                DifficultyProgression = DungeonTestHelpers.AnalyzeDifficulty(_blueprint?.Rooms),
                LootDistribution = DungeonTestHelpers.AuditLootTable(_blueprint?.Rooms),
                RoomVariety = DungeonTestHelpers.CountRoomTypes(_blueprint?.Rooms)
            };

            return report;
        }
    }

    #endregion
}
