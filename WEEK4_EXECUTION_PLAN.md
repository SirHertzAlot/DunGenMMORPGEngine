# Week 4 Execution Plan: Procedural Generation Depth

**Duration**: May 13 - May 19, 2026 (1 week)
**Goal**: Complete dungeon generation with encounter composition and difficulty progression
**Success**: All Tier 2 content acceptance tests pass; generation is production-grade

---

## Overview

Week 4 transforms procedural generation from a basic scaffold into a full content engine. The focus is:

1. **Advanced room generation** — 5+ room types with strategic placement
2. **Enemy composition** — Weighted spawning by difficulty tier
3. **Loot distribution** — Rarity-based tables tied to progression
4. **Validation framework** — Ensure generated dungeons are playable and balanced

By the end of Week 4, a single seed should generate a complete, playable, balanced dungeon.

---

## Daily Breakdown

### Day 1 (Mon 5/13): Planning & Test Scaffolding

#### Morning: Set Up Test Suite
- Create `tests/Week4_ProceduralGenerationTests.cs`
- Define test fixtures for dungeon validation
- Implement helpers for difficulty analysis and loot auditing

#### Tasks
- [ ] Create 5 test classes (RoomGeneration, EnemyComposition, LootDistribution, DungeonValidation, DeterminismTests)
- [ ] Implement `GeneratedDungeon` fixture (load, validate, analyze)
- [ ] Add helper methods: `AnalyzeDifficulty()`, `AuditLootTable()`, `CountRoomTypes()`

#### Acceptance
```csharp
[TestFixture]
public class RoomGenerationTests
{
    private DungeonGenerator _generator;
    
    [SetUp]
    public void Setup() => _generator = new DungeonGenerator(seed: 42);
    
    [Test]
    public void GenerateRooms_ProducesAtLeastFiveRoomTypes() { }
    
    [Test]
    public void RoomPlacement_RoomsAreConnected() { }
}
```

#### Deliverable
- Test scaffolding PR (tests fail initially, which is expected)

---

### Day 2 (Tue 5/14): Room Generation & Connectivity

#### Goal: Implement diverse room types with valid connectivity

#### Tasks
- [ ] Define `RoomType` enum (Treasure, Encounter, Trap, Puzzle, Boss)
- [ ] Implement `RoomGenerator.CreateRoom(type, seed)` → Room with prefab, difficulty, dimensions
- [ ] Add `DungeonConnectivity.ValidateConnections()` — ensures no dead ends
- [ ] Implement constraint-based placement (e.g., boss room must be reachable from start)

#### Code Structure
```csharp
public enum RoomType { Treasure, Encounter, Trap, Puzzle, Boss }

public class Room
{
    public RoomType Type { get; set; }
    public int Difficulty { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<Exit> Exits { get; set; }
    public LootTableIndex LootTable { get; set; }
}

public class DungeonGenerator
{
    public Dungeon GenerateRooms(int seed, int roomCount)
    {
        var rng = new DeterministicRNG(seed);
        var rooms = new List<Room>();
        
        // Generate room types weighted by rarity
        foreach (var type in GetWeightedRoomTypes(rng))
            rooms.Add(RoomGenerator.CreateRoom(type, rng));
        
        return new Dungeon { Rooms = rooms };
    }
    
    private List<RoomType> GetWeightedRoomTypes(DeterministicRNG rng)
    {
        // Boss: 1, Treasure: 2, Encounter: 5, Trap: 2, Puzzle: 1
    }
}
```

#### Acceptance Tests
```csharp
[Test]
public void RoomGeneration_CreatesAllFiveTypes()
{
    var dungeon = _generator.GenerateRooms(seed: 42, roomCount: 20);
    var types = dungeon.Rooms.Select(r => r.Type).Distinct();
    Assert.AreEqual(5, types.Count());
}

[Test]
public void RoomConnectivity_HasNoDeadEnds()
{
    var dungeon = _generator.GenerateRooms(seed: 42, roomCount: 20);
    var validator = new DungeonValidator(dungeon);
    Assert.IsTrue(validator.AllRoomsConnected());
}

[Test]
public void RoomGeneration_IsDeterministic()
{
    var d1 = _generator.GenerateRooms(seed: 42, roomCount: 20);
    var d2 = _generator.GenerateRooms(seed: 42, roomCount: 20);
    
    CollectionAssert.AreEqual(
        d1.Rooms.Select(r => r.Type),
        d2.Rooms.Select(r => r.Type)
    );
}
```

#### Deliverable
- Room generation with validation passing

---

### Day 3 (Wed 5/15): Enemy Composition & Difficulty Scaling

#### Goal: Place enemies in encounters using weighted difficulty curves

#### Tasks
- [ ] Implement `EncounterBuilder.CreateEncounter(roomType, difficulty, seed)`
- [ ] Add weighted enemy selection (e.g., trash can have 1-3 enemies, boss is always 1 legendary)
- [ ] Implement difficulty calculation (enemy level + count)
- [ ] Add difficulty progression curve (flat early, steep late)

#### Code Structure
```csharp
public class EncounterBuilder
{
    private readonly DeterministicRNG _rng;
    
    public Encounter CreateEncounter(RoomType type, int difficultyTier, DeterministicRNG rng)
    {
        var encounter = new Encounter { RoomType = type, Difficulty = difficultyTier };
        
        switch (type)
        {
            case RoomType.Encounter:
                encounter.Enemies = SpawnTrashEncounter(difficultyTier, rng);
                break;
            case RoomType.Boss:
                encounter.Enemies = SpawnBossEncounter(difficultyTier, rng);
                break;
            case RoomType.Treasure:
                encounter.Enemies = new List<Enemy>(); // No combat
                break;
        }
        
        return encounter;
    }
    
    private List<Enemy> SpawnTrashEncounter(int tier, DeterministicRNG rng)
    {
        int enemyCount = rng.DiceRoll(3); // 1-3 enemies
        var enemies = new List<Enemy>();
        
        for (int i = 0; i < enemyCount; i++)
        {
            int level = Math.Max(1, tier + rng.DiceRoll(3) - 2);
            enemies.Add(EnemyFactory.Create(level, rng));
        }
        
        return enemies;
    }
    
    private List<Enemy> SpawnBossEncounter(int tier, DeterministicRNG rng)
    {
        int level = Math.Min(10, tier + 3); // Boss is stronger
        return new List<Enemy> { EnemyFactory.CreateBoss(level, rng) };
    }
}
```

#### Acceptance Tests
```csharp
[Test]
public void TrashEncounter_HasOneToThreeEnemies()
{
    var encounter = _builder.CreateEncounter(RoomType.Encounter, difficulty: 3, rng);
    Assert.IsTrue(encounter.Enemies.Count >= 1 && encounter.Enemies.Count <= 3);
}

[Test]
public void BossEncounter_HasOneEnemy()
{
    var encounter = _builder.CreateEncounter(RoomType.Boss, difficulty: 5, rng);
    Assert.AreEqual(1, encounter.Enemies.Count);
}

[Test]
public void EnemyDifficulty_ScalesWithTier()
{
    var e3 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 3, new DeterministicRNG(42));
    var e7 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 7, new DeterministicRNG(42));
    
    var avgLevel3 = e3.Enemies.Average(e => e.Level);
    var avgLevel7 = e7.Enemies.Average(e => e.Level);
    
    Assert.Less(avgLevel3, avgLevel7);
}

[Test]
public void EnemyComposition_IsDeterministic()
{
    var e1 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 4, new DeterministicRNG(42));
    var e2 = _builder.CreateEncounter(RoomType.Encounter, difficulty: 4, new DeterministicRNG(42));
    
    CollectionAssert.AreEqual(
        e1.Enemies.Select(e => e.Level),
        e2.Enemies.Select(e => e.Level)
    );
}
```

#### Deliverable
- Enemy composition and difficulty scaling validated

---

### Day 4 (Thu 5/16): Loot Distribution & Progression Curves

#### Goal: Create rarity-based loot tables tied to dungeon difficulty

#### Tasks
- [ ] Implement `LootTableFactory.CreateTable(roomType, difficulty)` → LootTable with rarity weights
- [ ] Add `ItemRarity` enum (Common, Uncommon, Rare, Epic, Legendary)
- [ ] Implement loot generation from tables (weighted rolls)
- [ ] Validate loot progression (difficulty 1-10 sees increasing rarity)

#### Code Structure
```csharp
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

public class LootTable
{
    public Dictionary<ItemRarity, List<ItemTemplate>> RarityTiers { get; set; }
    public Dictionary<ItemRarity, float> RarityWeights { get; set; } // e.g., Common: 70%, Uncommon: 20%, etc.
}

public class LootTableFactory
{
    public LootTable CreateTable(RoomType type, int difficulty)
    {
        var table = new LootTable();
        
        switch (type)
        {
            case RoomType.Treasure:
                table.RarityWeights = new() { 
                    { ItemRarity.Common, 0.3f },
                    { ItemRarity.Uncommon, 0.4f },
                    { ItemRarity.Rare, 0.2f },
                    { ItemRarity.Epic, 0.1f }
                };
                break;
            case RoomType.Boss:
                table.RarityWeights = new() { 
                    { ItemRarity.Uncommon, 0.2f },
                    { ItemRarity.Rare, 0.4f },
                    { ItemRarity.Epic, 0.3f },
                    { ItemRarity.Legendary, 0.1f }
                };
                break;
        }
        
        // Scale rarity by difficulty
        foreach (var rarity in table.RarityWeights.Keys.ToList())
        {
            float scale = 1.0f + (difficulty * 0.05f);
            table.RarityWeights[rarity] *= scale;
        }
        
        // Normalize weights
        float total = table.RarityWeights.Values.Sum();
        foreach (var key in table.RarityWeights.Keys.ToList())
            table.RarityWeights[key] /= total;
        
        return table;
    }
    
    public Item RollLoot(LootTable table, DeterministicRNG rng)
    {
        int roll = rng.DiceRoll(100);
        ItemRarity selected = SelectRarity(table, roll);
        return SelectItemFromRarity(table, selected, rng);
    }
}
```

#### Acceptance Tests
```csharp
[Test]
public void TreasureRoom_HasHighCommonRarity()
{
    var table = _factory.CreateTable(RoomType.Treasure, difficulty: 3);
    Assert.Greater(table.RarityWeights[ItemRarity.Common], 0.25f);
}

[Test]
public void BossRoom_HasHighEpicRarity()
{
    var table = _factory.CreateTable(RoomType.Boss, difficulty: 8);
    Assert.Greater(table.RarityWeights[ItemRarity.Epic], 0.20f);
}

[Test]
public void LootProgression_DifficultyIncreasesRarity()
{
    var table1 = _factory.CreateTable(RoomType.Treasure, difficulty: 1);
    var table10 = _factory.CreateTable(RoomType.Treasure, difficulty: 10);
    
    var common1 = table1.RarityWeights[ItemRarity.Common];
    var common10 = table10.RarityWeights[ItemRarity.Common];
    
    Assert.Greater(common1, common10); // Less common loot at high difficulty
}

[Test]
public void LootGeneration_IsDeterministic()
{
    var table = _factory.CreateTable(RoomType.Treasure, difficulty: 5);
    var item1 = _factory.RollLoot(table, new DeterministicRNG(42));
    var item2 = _factory.RollLoot(table, new DeterministicRNG(42));
    
    Assert.AreEqual(item1.Id, item2.Id);
}
```

#### Deliverable
- Loot tables and progression curves validated

---

### Day 5 (Fri 5/17): Integration & Validation Framework

#### Goal: Integrate all systems and validate generated dungeons are balanced and playable

#### Tasks
- [ ] Create `DungeonValidator` that checks all metrics
- [ ] Implement `BalanceAnalyzer` — difficulty curve, loot distribution, encounter variety
- [ ] Add `ReplayabilityAnalyzer` — ensures generation is deterministic across 100 seeds
- [ ] Generate 100 test dungeons, audit results, report

#### Code Structure
```csharp
public class DungeonValidator
{
    private readonly Dungeon _dungeon;
    
    public ValidationReport Validate()
    {
        var report = new ValidationReport();
        
        report.IsConnected = AllRoomsConnected();
        report.HasBoss = ContainsBossRoom();
        report.HasTreasure = ContainsTreasureRoom();
        report.DifficultyProgression = AnalyzeDifficulty();
        report.LootDistribution = AnalyzeLoot();
        report.RoomVariety = CountRoomTypes();
        
        return report;
    }
    
    private bool AllRoomsConnected() { /* BFS validation */ }
    private bool ContainsBossRoom() => _dungeon.Rooms.Any(r => r.Type == RoomType.Boss);
    private bool ContainsTreasureRoom() => _dungeon.Rooms.Any(r => r.Type == RoomType.Treasure);
    
    private DifficultyProgression AnalyzeDifficulty()
    {
        return new DifficultyProgression
        {
            RoomDifficulties = _dungeon.Rooms.Select(r => r.Difficulty).ToList(),
            IsProgressive = _dungeon.Rooms.Select(r => r.Difficulty).SequenceEqual(
                _dungeon.Rooms.Select(r => r.Difficulty).OrderBy(d => d)
            )
        };
    }
}

[Test]
public void GenerateDungeons_100Seeds_AllValid()
{
    var results = new List<ValidationReport>();
    
    for (int seed = 0; seed < 100; seed++)
    {
        var dungeon = _generator.GenerateRooms(seed, roomCount: 15);
        var validator = new DungeonValidator(dungeon);
        results.Add(validator.Validate());
    }
    
    Assert.AreEqual(100, results.Count(r => r.IsValid));
    Assert.GreaterOrEqual(results.Count(r => r.HasBoss), 100); // All have boss
}

[Test]
public void DungeonGeneration_IsDeterministic_Across100Seeds()
{
    for (int seed = 0; seed < 100; seed++)
    {
        var d1 = _generator.GenerateRooms(seed, roomCount: 15);
        var d2 = _generator.GenerateRooms(seed, roomCount: 15);
        
        CollectionAssert.AreEqual(
            d1.Rooms.Select(r => r.Type),
            d2.Rooms.Select(r => r.Type)
        );
    }
}
```

#### Deliverable
- Dungeon validation framework + 100-seed audit report

---

## Completion Checklist

By EOD Friday, all of the following must be true:

- [ ] 5 room types are implemented and placed correctly
- [ ] Enemy encounters scale with difficulty
- [ ] Loot tables match progression curves
- [ ] 100 generated dungeons pass validation (all connected, balanced, playable)
- [ ] Generation is deterministic (same seed = same dungeon)
- [ ] All acceptance tests pass
- [ ] No regressions in existing tests (EditMode, PlayMode, backend)
- [ ] Performance is acceptable (< 500ms per dungeon generation)

---

## Integration Points

- **Dungeon persistence**: Save/load generated dungeons to YAML
- **Content lookup**: UI can query available items/enemies by rarity
- **Progression validation**: Loot progression feeds into character leveling

---

## Success Metrics

| Metric | Target | Actual |
|--------|--------|--------|
| Room types | 5 | — |
| Valid dungeons | 100/100 | — |
| Determinism | 100% | — |
| Generation time | < 500ms | — |
| Test coverage | 100% of generation | — |
| Regressions | 0 | — |

