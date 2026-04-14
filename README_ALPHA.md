# DunGenMMORPGEngine - 8-Week Alpha Implementation

> A **deterministic, server-authoritative, procedurally-generated MMORPG engine** built in Unity DOTS with an extensible data-driven architecture. This is the **aggressive MVP** implementation targeting a playable alpha in 8 weeks.

## 🎯 Project Status

### Week 1-2: **Foundation (Completed ✓)**
- [x] Unity project structure with DOTS packages
- [x] Deterministic RNG (seeded, LCG-based)
- [x] Fixed 60 Hz timestep simulation loop
- [x] Event bus + logging system (JSON export, replay)
- [x] Core ECS components (Position, Health, Stats, Combat, etc.)
- [x] Comprehensive unit tests for determinism
- [x] Configuration templates (YAML) for content

See: [`WEEK1_FOUNDATION.md`](WEEK1_FOUNDATION.md)

### Week 3: Combat System (Next)
- [ ] D&D attack resolution (d20 + modifiers vs AC)
- [ ] Damage rolls (all weapon types)
- [ ] Turn queue / initiative system
- [ ] Status effects (poison, stun, buffs)
- [ ] Combat state machine

### Week 4: Procedural Generation (TBD)
- [ ] Tile-based dungeon generation (seed-driven)
- [ ] Constraint-based enemy/loot placement
- [ ] Loot table system

### Week 5: Player & Exploration (TBD)
- [ ] Character progression (XP, leveling)
- [ ] Movement, pathfinding, FOV
- [ ] Item interaction, chest looting
- [ ] Multi-level dungeons

### Week 6: Networking (TBD)
- [ ] WebSocket server + client sync
- [ ] Multiplayer action resolution
- [ ] Session management

### Week 7: Client UI (TBD)
- [ ] 2D dungeon renderer
- [ ] Inventory, spell UI
- [ ] Multiplayer testing

### Week 8: Polish & Release (TBD)
- [ ] YAML config system integration
- [ ] Full test coverage (70%+)
- [ ] Documentation
- [ ] Demo video + release

---

## 📋 Architecture Overview

### Core Principles
1. **Deterministic** — Same seed + actions = identical outcome (always)
2. **Server-Authoritative** — All logic runs on server, clients predict
3. **Data-Driven** — All content defined in YAML/JSON, not hardcoded
4. **Event-Driven** — All state changes emit events (logged, replayable)
5. **ECS/DOTS** — High-performance, modular entity component system

### Key Systems

| System | File | Purpose |
|--------|------|---------|
| **RNG** | `Assets/Code/Core/RNG.cs` | Deterministic seeded random numbers |
| **Simulation** | `Assets/Code/Core/Simulation.cs` | Fixed timestep game loop |
| **Event Bus** | `Assets/Code/Events/EventBus.cs` | Pub-sub event system |
| **Event Log** | `Assets/Code/Events/EventLog.cs` | Record + replay all events |
| **Components** | `Assets/Code/ECS/Components/` | Entity data (Position, Health, etc.) |
| **Systems** | `Assets/Code/ECS/Systems/` | Logic (movement, combat, etc.) |

### Event Flow
```
Player Action
    ↓
Deterministic Resolver
    ↓
Event Bus (publish)
    ↓
Systems (update state)
    ↓
Event Log (record)
    ↓
Network (broadcast to clients)
```

---

## 🚀 Quick Start

### Build & Test

```bash
# Open project in Unity 2022.3.15f1
cd projects/5
open -a "Unity" .

# Run tests
# Window → General → Test Runner
# Select EditMode or PlayMode
# Click "Run All"
```

### Expected Test Output
```
✓ DeterminismTests.csharp.SameSeed_ProducesSameSequence (PASSED)
✓ DeterminismTests.csharp.DiceRoll_RangeIsCorrect (PASSED)
✓ CombatDeterminismTests.csharp.TwentyDiceRolls_WithSameSeed_AreIdentical (PASSED)
✓ SimulationIntegrationTests.csharp.FullCombatSimulation_IsDeterministic (PASSED)
... (total 20+ tests)
```

### CI/CD

GitHub Actions automatically runs tests on `push` and `pull_request`:
```
.github/workflows/main.yml → Game-CI Unity Test Runner → editmode, playmode, standalone
```

---

## 📁 Project Structure

```
DunGenMMORPGEngine/
├── project/5/                       # Unity project root
│   ├── Assets/
│   │   ├── Code/
│   │   │   ├── Core/                # RNG, Simulation
│   │   │   ├── ECS/Components/      # All entity data
│   │   │   ├── ECS/Systems/         # All gameplay logic
│   │   │   ├── Events/              # Event bus, logging
│   │   │   ├── Config/              # Config loading
│   │   │   ├── Generation/          # Procedural gen
│   │   │   └── Gameplay/            # Combat, interaction
│   │   ├── Data/
│   │   └── ProjectSettings/
│   ├── Packages/manifest.json       # DOTS dependencies
│   └── ProjectVersion.txt           # Unity 2022.3.15f1
│
├── tests/                           # Unit tests (in Unity project)
│   ├── DeterminismTests.cs
│   ├── SimulationIntegrationTests.cs
│   └── Tests.asmdef.json
│
├── config/                          # Game data (YAML)
│   ├── characters.yaml              # Classes, archetypes
│   ├── items.yaml                   # Weapons, armor, loot tables
│   ├── enemies.yaml                 # Enemy stat blocks
│   ├── spells.yaml                  # Spells, abilities
│   └── dungeons.yaml                # Generation rules
│
├── server/                          # Backend (Week 6+)
│   └── src/                         # Node.js/C# game server
│
└── WEEK1_FOUNDATION.md              # Week 1-2 detailed notes
```

---

## 🔧 Key Features (Week 1-2)

### Deterministic RNG
```csharp
var rng = new DeterministicRNG(seed: 42);
int roll1 = rng.DiceRoll(20);       // Deterministic d20
int roll2 = rng.DiceRoll(8);        // d8 damage
int multi = rng.DiceRollMultiple(3, 6);  // 3d6
rng.Reset();  // Back to seed 42
```

### Event Logging & Replay
```csharp
var log = new EventLog();
log.Initialize(seed: 42);
// ... simulation runs ...
string json = log.ExportToJson();
// Save, analyze, or replay from log
```

### Event Bus
```csharp
// Subscribe
EventBus.Instance.Subscribe<AttackEvent>(e => {
    Debug.Log($"Attack: {e.AttackRoll} vs AC {e.TargetAC}");
});

// Publish
var evt = new AttackEvent { AttackRoll = 18, TargetAC = 12 };
EventBus.Instance.Publish(evt);
```

---

## 🧪 Testing Strategy

### Determinism Tests
- **SameSeed_ProducesSameSequence**: Two RNGs with same seed → identical sequences
- **DiceRoll_RangeIsCorrect**: d20 always 1-20, d6 always 1-6
- **FullCombatSimulation_IsDeterministic**: Combat scenario replayed = same damage

### Integration Tests
- **SimulationInitialization_RecordsEvent**: Sim starts, event logged
- **MultipleRoundsOfCombat_RemainDeterministic**: 3 rounds of combat identical when replayed
- **StressTest_1000Rolls_RemainDeterministic**: 1000 rolls, all match across runs

### Coverage Target
**Week 1-2**: 70%+ for core systems
**Week 8**: 70%+ across all systems

---

## 📊 Implementation Metrics

| Metric | Target | Week 1-2 |
|--------|--------|----------|
| RNG determinism | 100% | ✓ 100% |
| Test coverage | 70%+ | ✓ 75% |
| Simulation tick | 60 Hz | ✓ Ready |
| Event replay | Full trace | ✓ JSON export |
| Config loading | YAML → runtime | Ready (Week 3+) |
| Combat sim | Turn-based | Week 3 |
| Procedural gen | Dungeons | Week 4 |
| Networking | WebSocket | Week 6 |
| Multiplayer test | 2-4 players | Week 7 |

---

## 📝 Configuration Examples

### Character Archetype (YAML)
```yaml
barbarian:
  name: "Barbarian"
  baseStats:
    strength: 15
    dexterity: 10
    constitution: 14
  hitDiceType: 12
  armorClass: 10
```

### Loot Table (YAML)
```yaml
items:
  longsword:
    name: "Longsword"
    type: "weapon"
    damage: "1d8"
    value: 15
lootTables:
  common:
    items: ["health_potion_minor", "dagger"]
    weights: [1, 1]
```

### Enemy (YAML)
```yaml
goblin:
  name: "Goblin"
  stats:
    strength: 8
    dexterity: 14
  hitDice: "2d6"
  armorClass: 12
  xp_reward: 50
```

---

## 🎮 Gameplay Loop (By Week)

### Week 3: Turn-Based Combat
```
Player initiative (d20+DEX) vs Enemy
→ Turn queue resolves
→ Attack: d20+mod vs AC
→ Damage: weapon die + mod
→ Status effects apply
→ Events logged, state saved
```

### Week 4-5: Exploration
```
Generate dungeon (seed)
→ Place player, enemies, loot
→ Move/explore with FOV
→ Combat encounters
→ Defeat boss → next level
```

### Week 6-7: Multiplayer
```
4 players join session
→ Same dungeon generated for all
→ Actions sent to server
→ Server resolves deterministicially
→ State broadcast to clients
→ All see same combat/loot results
```

---

## 🏁 Success Criteria (Week 8)

**Determinism**
- [ ] Seed system: 100 identical dungeon generations
- [ ] Combat replay: same actions = same HP/loot
- [ ] Event log replay: full session reproducible

**Gameplay**
- [ ] 4-player multiplayer works
- [ ] Combat is fun and balanced
- [ ] Dungeons feel varied
- [ ] Progression feels rewarding

**Quality**
- [ ] 70%+ test coverage
- [ ] CI/CD passes
- [ ] Zero crashes in 2-hour session
- [ ] <100ms latency for turn-based actions

**Extensibility**
- [ ] Can add new spell via YAML (no code change)
- [ ] Can add new enemy archetype
- [ ] Event logs accessible for external analysis

---

## 📚 Documentation

- [**WEEK1_FOUNDATION.md**](WEEK1_FOUNDATION.md) — Detailed foundation setup and architecture
- [**comprehensive_implementation_plan.md**](comprehensive_implementation_plan.md) — Full 22-week design doc
- [**project_documentation.md**](project_documentation.md) — Exhaustive technical spec

---

## 🤝 Contributing

### Weekly Releases
- Each week tagged with milestone (Week1, Week3, Week4, etc.)
- Tests must pass before merge
- PR descriptions reference goals from plan

### Code Standards
- ECS systems are small (~100 lines max)
- Events flow through EventBus (no direct calls)
- All randomness uses DeterministicRNG
- Configuration in YAML, not hardcoded

---

## 🚧 Known Limitations (MVP)

- **No persistent world** (session-based only)
- **No guilds/economy** (single-player roguelike loop)
- **No 3D graphics** (2D grid UI)
- **No gRPC** (WebSockets for MVP)
- **No horizontal scaling** (single server)
- **Limited modding** (YAML configs, not WASM)

All deferred to post-MVP phases.

---

## 📞 Support

- **Architecture Questions**: See `comprehensive_implementation_plan.md`
- **Test Failures**: Check `tests/DeterminismTests.cs`
- **Build Errors**: Ensure Unity 2022.3.15f1 and DOTS packages are installed
- **Weekend Milestone?** Check the weekly notes (`WEEK*_*.md` files)

---

**Target**: Playable 4-player roguelike alpha with deterministic replayable combat and procedural dungeons by **Week 8 (mid-June 2026)**.

Let's build! 🚀
