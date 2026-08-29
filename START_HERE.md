# DunGenMMORPGEngine: Start Here

**Status**: Validated alpha track with canonical status in [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md)

---

## 📖 Quick Navigation

**New to this project?** Start here:
1. Read [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md) first — canonical execution plan and current priorities
2. Read [README_ALPHA.md](README_ALPHA.md) second — validated roadmap snapshot and alpha gates
3. Run [HOW_TO_RUN_LOCALLY.md](HOW_TO_RUN_LOCALLY.md) to set up and verify locally

**Need the current source-of-truth status and next priorities?**
→ Read [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md)

**Want the full executive summary?**
→ See [WEEK1_COMPLETE.md](WEEK1_COMPLETE.md) (historical milestone report; not the live plan)

**Want implementation details?**
→ See [IMPLEMENTATION_STATUS_WEEK1.md](IMPLEMENTATION_STATUS_WEEK1.md) (historical checklist + metrics)

---

## What Has Been Delivered

### Core Implementation
✅ **Deterministic RNG** — Seeded, reproducible, validated
✅ **Fixed 60 Hz timestep** — Simulation loop in place
✅ **Event system** — Logging and replay primitives
✅ **ECS components** — Core entity data and gameplay scaffolding
✅ **Configuration** — YAML content loader and content templates
✅ **Test suite** — Unity EditMode, Unity PlayMode, and authoritative backend coverage

### Documentation
✅ Canonical status and planning docs are identified
✅ Historical milestone docs are retained for context
✅ Local setup and validation guidance exists

### Quality
✅ Validated test slices are green
✅ Deterministic foundation is in place
✅ Documentation has a defined source of truth

---

## File Structure

```
Assets/DunGenMMORPGEngine/   ← Project docs, content, and imported support assets
├── README_ALPHA.md           ← Validated roadmap snapshot
├── OBJECTIVES_REALIGNMENT_PLAN.md ← Canonical execution plan
├── HOW_TO_RUN_LOCALLY.md     ← Setup and verification guide
├── config/                   ← YAML content and tooling
└── ported-from-zip-unmodified/ ← Imported support/template material

Unity project and backend code live elsewhere in the workspace and are referenced by the canonical docs above.

Historical milestone docs remain in this folder for reference, but they are not the live status dashboard.
```

---

## Quick Verification

**To verify everything works locally:**

```bash
# 1. Open the Unity project from the workspace root
# 2. Run EditMode and PlayMode tests in Unity
# 3. Run the authoritative backend tests from the backend test project
```

See [HOW_TO_RUN_LOCALLY.md](HOW_TO_RUN_LOCALLY.md) for detailed instructions and current validation commands.

---

## Test Results

Validated slices are green:

- Unity PlayMode: 122/122 passed
- Unity EditMode: 11/11 passed
- Authoritative backend tests: 6/6 passed

## Key Concepts

### DeterministicRNG
```csharp
var rng = new DeterministicRNG(seed: 42);
int d20 = rng.DiceRoll(20);  // Deterministic d20

// Reset
rng.Reset();
int d20_again = rng.DiceRoll(20);  // Same value!
```

### Event System
```csharp
// Subscribe
EventBus.Instance.Subscribe<AttackEvent>(e => {
    Debug.Log($"Attack: {e.AttackRoll} vs AC {e.TargetAC}");
});

// Publish
EventBus.Instance.Publish(new AttackEvent { ... });
```

### Event Logging
```csharp
EventLog log = sim.GetEventLog();
string json = log.ExportToJson();  // Save for replay/analysis
```

---

## The 8-Week Plan

| Week | Focus | Status |
|------|-------|--------|
| **1-2** | **Foundation** | ✅ **Implemented** |
| 3 | Combat system | ✅ **Implemented** |
| 4 | Procedural generation | 🟡 **Partially implemented** |
| 5 | Player & exploration | 🟡 **Partially implemented** |
| 6 | Networking | 🟡 **In progress** |
| 7 | Client UI | ⬜ **Planned** |
| 8 | Polish & release | ⬜ **Planned** |

The canonical alpha exit gates are defined in [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md).

---

## For Next Developer

### Before You Start
1. Read [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md)
2. Read [README_ALPHA.md](README_ALPHA.md)
3. Run the local verification steps above

### Key Points
- Keep the canonical docs in sync with implementation.
- Preserve determinism and replayability.
- Prefer data-driven content over hardcoded values.
- Tie every feature claim to tests or a reproducible validation path.

### Next Implementation Focus
1. Close the minimal authoritative multiplayer slice.
2. Add a deterministic shared encounter and replay log.
3. Expose session state and encounter outcome through a minimal client flow.

---

## Success Criteria Met

- [x] Deterministic foundation established
- [x] Event system supports logging and replay
- [x] Configuration templates exist
- [x] Validated automated tests are passing on the checked slices
- [x] Canonical planning docs now point to the same source of truth

---

## By the Numbers

- Core engine, backend, and template assets are present across the workspace
- Validation has been run on Unity and backend slices
- Canonical status now lives in the two plan docs above

---

## Next Steps

### For Contributors
1. Read the canonical plan in [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md).
2. Use [README_ALPHA.md](README_ALPHA.md) for the validated alpha snapshot.
3. Keep implementation claims tied to tests or a reproducible validation path.

### For the Project
- Close the minimal authoritative multiplayer slice.
- Add deterministic shared encounter replay validation.
- Keep roadmap docs synchronized with scope changes.

---

## Questions?

See the appropriate guide:
- **Canonical status**: [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md)
- **Validated roadmap**: [README_ALPHA.md](README_ALPHA.md)
- **Setup and verification**: [HOW_TO_RUN_LOCALLY.md](HOW_TO_RUN_LOCALLY.md)
- **Historical milestone context**: [WEEK1_COMPLETE.md](WEEK1_COMPLETE.md)

---

## Summary

This project has a validated deterministic foundation, but the alpha is not complete until the multiplayer exit gates in [OBJECTIVES_REALIGNMENT_PLAN.md](OBJECTIVES_REALIGNMENT_PLAN.md) are met.

🚀 **Let's build!**
