# Running Week 1-2 Implementation Locally

**Goal**: Verify the deterministic simulation is working correctly.

---

## ✅ Prerequisites

- **Unity 2022.3.15f1** (Download from [unity.com](https://unity.com/download))
- **Git** (to clone repository)
- **Text editor** for reviewing YAML config files (VS Code recommended)

---

## 🚀 Setup Steps

### 1. Clone / Open Repository
```bash
git clone https://github.com/SirHertzAlot/DunGenMMORPGEngine.git
cd DunGenMMORPGEngine
```

### 2. Open Project in Unity

```bash
# macOS
open -a "Unity" projects/5

# Windows
"C:\Program Files\Unity\Hub\Editor\2022.3.15f1\Editor\Unity.exe" -projectPath projects\5

# Linux
/opt/Unity/Editor/Unity -projectPath projects/5
```

**Expected**: Unity opens, automatically imports DOTS packages (may take 2-5 min)

### 3. Run Tests (EditMode)

1. **Window → General → Test Runner**
2. Click **EditMode** tab
3. Click **Run All**

**Expected Output**:
```
Test Run Started
...
EditMode:
  DeterminismTests.SameSeed_ProducesSameSequence (PASSED)
  DeterminismTests.DifferentSeeds_ProduceDifferentSequences (PASSED)
  ... (22 tests total)
Test Run Finished: 22 Passed, 0 Failed

Overall Result: PASSED ✓
```

### 4. Run Tests (PlayMode)

1. **Test Runner → PlayMode** tab
2. Click **Run All**

**Expected**: Same 22 tests pass in PlayMode

---

## 🎮 Run Simulation in Editor

### Option A: Play Mode Test
1. **Scenes** folder → Create new scene `TestSimulation.unity`
2. **GameObject → Create Empty** (name it `SimulationRunner`)
3. **Add Component → SimulationStarter**
4. Set `Simulation Seed = 12345`
5. **Press Play**

**Expected**: Console shows:
```
✓ Simulation initialized with seed: 12345
✓ Event Bus ready
✓ Event Log started
```

Will see on-screen debug info:
```
Simulation Running: True
Frame: 345
Seed: 12345
Events: 1
```

### Option B: Standalone Build
1. **File → Build Settings**
2. Select **Linux/Windows/macOS**
3. **Build and Run**

---

## 🔍 Verify Determinism

### Verify Same Seed = Same Sequence

```cpp
// In C# or Unity REPL
var rng1 = new DeterministicRNG(42);
var rng2 = new DeterministicRNG(42);

for (int i = 0; i < 10; i++) {
    int r1 = rng1.DiceRoll(20);
    int r2 = rng2.DiceRoll(20);
    Debug.Log($"Roll {i}: {r1} == {r2} ? {r1 == r2}");
    // Output: Roll 0: 17 == 17 ? True
    // ...
}
```

### Export Event Log

```cpp
// In SimulationStarter, click "Export Log" button
// Or manually:
Simulation sim = GetComponent<SimulationStarter>().GetSimulation();
string json = sim.ExportLog();
Debug.Log(json);
```

**Expected**: JSON output shows all events with frame numbers and seeds.

---

## 📊 Review Configuration

All game content is in `/config/`:

### View Character Classes
```bash
cat config/characters.yaml
```
Expected: 4 classes (Barbarian, Rogue, Cleric, Wizard) with stats

### View Loot Tables
```bash
cat config/items.yaml
```
Expected: 11+ items, rarity system (common → legendary)

### View Enemies
```bash
cat config/enemies.yaml
```
Expected: 6 enemies with full stat blocks, XP rewards

### View Spells
```bash
cat config/spells.yaml
```
Expected: 8 spells with mana costs, damage formulas

---

## 🧪 Run Individual Tests

### Test Specific Class
```
Test Runner → EditMode
Right-click DeterminismTests.cs → Run Selected
```

### Test Specific Method
```
Expand DeterminismTests → Right-click SameSeed_ProducesSameSequence → Run
```

### Watch Console Output
```
Window → General → Console
```

---

## 🐛 Troubleshooting

### Tests Won't Run
**Issue**: "No tests found"
- **Fix**: Right-click `tests/` folder → Reimport
- **Fix**: Check assembly definitions loaded: Assets → Code → [AssemblyName].asmdef.json

### Package Import Fails
**Issue**: "Missing package com.unity.entities"
- **Fix**: Window → Package Manager → Search "entities" → Install 1.0.15

### Simulation Crashes
**Issue**: NullReferenceException in Update()
- **Fix**: Check SimulationStarter is on a GameObject in scene
- **Fix**: Check Initialize() was called before SimulationStep()

### Git Errors
**Issue**: "Projects/5 directory too large"
- **Fix**: Run `git lfs install`
- **Fix**: Check `.gitignore` includes `projects/5/Library/`

---

## ✅ Verification Checklist

After running locally:

- [ ] Unity opens without errors
- [ ] All DOTS packages imported successfully
- [ ] 22/22 tests pass in EditMode
- [ ] 22/22 tests pass in PlayMode
- [ ] Play mode shows debug info on screen
- [ ] Event log JSON is valid
- [ ] Config YAML files are readable
- [ ] No compilation errors in Console

---

## 📈 Next Steps

Once verified locally:

1. **Review architecture**: `WEEK1_FOUNDATION.md`
2. **Plan Week 3 combat**: `comprehensive_implementation_plan.md` → Combat Section
3. **Check config structure**: Notice how `enemies.yaml` references `items.yaml`
4. **Propose improvements**: Open issue if you find bugs

---

## 🎯 What to Expect

### You Should See
- ✓ Deterministic dice rolls (same seed = same numbers)
- ✓ Event logs in JSON format
- ✓ All 22 tests passing
- ✓ YAML config files loaded correctly
- ✓ No compilation or runtime errors

### You Should NOT See
- ✗ Random variations in dice (determinism broken)
- ✗ Test failures (indicates core issue)
- ✗ Package import errors
- ✗ Event log with NaN or null values

---

## 📞 Common Questions

**Q: Where are the graphics/UI?**
A: Week 7. Week 1-2 is invisible simulation + tests.

**Q: Can I use this online yet?**
A: Not yet. Networking comes Week 6.

**Q: Where's the combat?**
A: Coming Week 3. Week 1-2 is foundations only.

**Q: How do I modify the RNG?**
A: Edit `DeterministicRNG.cs`. Any change will break determinism—tests will fail!

**Q: What if tests fail?**
A: Something broke determinism. Roll back changes and investigate.

---

**Total Time**: ~15 minutes to verify everything works locally.
**Expected Result**: All tests pass, simulation runs, configs load.

🚀 Ready for Week 3!
