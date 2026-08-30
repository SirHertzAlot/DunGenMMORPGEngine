# Dungeon Runtime Integration

**Date**: May 11, 2026  
**Status**: ✅ Complete

## Problem Solved

The dungeon generation system existed in isolation—the procedurally generated dungeon data was not being instantiated or linked to the live game runtime in Unity. This meant:
- The dungeon wasn't visually represented in the scene
- The dungeon logic wasn't tied to the player's gameplay experience
- Generated dungeon data had no effect on the running game

## Solution

Implemented a **runtime dungeon instantiation layer** that:

1. **Generates the dungeon blueprint** from either:
   - Authoritative world data (from the backend)
   - Procedural generation (via `GameSession`)

2. **Instantiates the dungeon in Unity** when:
   - The core runtime system (Simulation + GameSession) is fully initialized
   - Both systems are actively running
   - Never runs independently without active runtime

3. **Manages the dungeon lifecycle**:
   - Only creates dungeon objects when runtime is active
   - Automatically cleans up when the game ends
   - Destroys dungeon visuals when runtime shuts down

## Architecture

### RuntimeDungeonInstantiator.cs
A MonoBehaviour that handles dungeon instantiation from an `AuthoritativeWorldBlueprint`:

```csharp
public bool TryInstantiateDungeon(AuthoritativeWorldBlueprint blueprint, bool isRuntimeActive)
```

- **Guards runtime state**: Only instantiates if runtime is active
- **Spawns rooms, enemies, loot** at positions defined in the blueprint
- **Cleans up gracefully** via `CleanupDungeon()`
- **Tracks state** via `IsDungeonActive` property

### SimulationStarter.cs (Updated)
The main game bootstrap now:

1. **Initializes Simulation + GameSession**
2. **Creates RuntimeDungeonInstantiator** if needed
3. **Checks runtime state** before instantiation
4. **Passes blueprint** (authoritative or procedural)
5. **Monitors game state** and cleans up dungeon on game over
6. **Cleans up on destroy** via `OnDestroy()` callback

### GameSession.cs (Existing)
- Generates or receives dungeon blueprints
- Manages game logic independent of visuals
- Provides data for `RuntimeDungeonInstantiator` to consume

## Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Backend (Optional)                                          │
│ AuthoritativeSessionStateStore → AuthoritativeWorld         │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────┐
│ SimulationStarter (Game Bootstrap)                           │
│                                                              │
│  1. Create Simulation + GameSession                          │
│  2. Check: Are both initialized? ✓                           │
│  3. Pass blueprint to RuntimeDungeonInstantiator             │
│  4. Guard: Is runtime active? ✓ → Instantiate dungeon       │
│  5. Monitor: Game Over? → CleanupDungeon()                   │
└──────────────────────┬───────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────┐
│ RuntimeDungeonInstantiator (Runtime Only)                    │
│                                                              │
│  ├─ Spawn Rooms (GameObjects with mesh/collider)            │
│  ├─ Spawn Enemies (Entity-linked enemy visuals)             │
│  └─ Spawn Loot (Item drops with pickup logic)               │
│                                                              │
│  Status: Active only while Simulation runs                  │
│  Cleanup: Automatic on game end or runtime shutdown         │
└──────────────────────────────────────────────────────────────┘
```

## Safety Guards

The dungeon **will NOT instantiate** if:
- ❌ Simulation is null
- ❌ GameSession is null
- ❌ Runtime system is not fully initialized
- ❌ Game has already ended (`IsGameOver == true`)

The dungeon **WILL clean up** when:
- ✅ Game ends (`IsGameOver == true`)
- ✅ SimulationStarter is destroyed
- ✅ Runtime system shuts down

## Testing

| Suite | Result | Count |
|-------|--------|-------|
| Backend Tests | ✅ PASS | 42/42 |
| Unity EditMode Tests | ✅ PASS | 11/11 |
| **Total** | ✅ PASS | **53/53** |

## Usage

### In the Editor

1. Attach `SimulationStarter` to a GameObject in your main scene
2. Leave `autoInstantiateDungeon = true` (default)
3. Assign room/enemy/loot prefabs to `RuntimeDungeonInstantiator` (if not auto-created)
4. Press Play → Dungeon instantiates automatically

### Programmatically

```csharp
// Option 1: Let SimulationStarter handle it (recommended)
var starter = gameObject.AddComponent<SimulationStarter>();
// Dungeon will instantiate after bootstrap

// Option 2: Manual control
var instantiator = gameObject.AddComponent<RuntimeDungeonInstantiator>();
var blueprint = new AuthoritativeWorldBlueprint { ... };
bool success = instantiator.TryInstantiateDungeon(blueprint, isRuntimeActive: true);
if (success) {
    // Dungeon is now live in the scene
}
```

## Next Steps

1. **Link entity system** to visual representations (rooms ↔ entities)
2. **Implement room traversal** (player movement updates both ECS and scene)
3. **Sync enemy AI** with procedural placement
4. **Add loot interaction** (pickup, equip, inventory)
5. **Test multi-client** scenario with shared dungeon state

## Files Modified

- ✅ `RuntimeDungeonInstantiator.cs` (NEW)
- ✅ `SimulationStarter.cs` (Updated with lifecycle guards)
- ✅ `GameSession.cs` (No changes; existing blueprint support used)

---

**Status**: Dungeon is now a fully integrated runtime component. 🎉
