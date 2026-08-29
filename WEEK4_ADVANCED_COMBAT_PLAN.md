# Week 4: Advanced Combat System - Implementation Plan

> Historical planning snapshot: this document reflects the original Week 4 plan.
> For current validated implementation status and priorities, see `OBJECTIVES_REALIGNMENT_PLAN.md` and `README_ALPHA.md`.

## 🎯 Objectives
1. **Action System** - Turn-based action economy
2. **Turn Queue** - Proper turn order management  
3. **Complex Turns** - Multiple actions per turn
4. **Conditions** - Simple status system
5. **Combat Phases** - Structured round lifecycle
6. **Event Integration** - Leverage data-oriented event system

---

## Architecture

### Core Components to Add
```csharp
// Action system
ActionQueueComponent       // Queue of pending actions  
ActionComponent           // Single action definition
ActionCostComponent       // Action economy (actions, bonus actions, reactions)

// Turn management  
TurnQueueComponent        // Ordered turn sequence
CombatRoundComponent      // Current round state

// Conditions/Status
ConditionComponent        // Active conditions tracking
ResourceComponent         // Resources (mana, stamina, etc)

// Combat state
CombatPhaseComponent      // Combat lifecycle (initiative, acting, resolution)
CombatStatisticsComponent // Combat statistics (damage dealt, etc)
```

### Systems to Add
```csharp
CombatInitializationSystem     // Phase 0: Setup
ActionSelectionSystem          // Phase 1a: Choose actions
ActionResolutionSystem         // Phase 1b: Execute actions
TurnTransitionSystem           // Phase 2: End turn
RoundTransitionSystem          // Phase 3: End round
CombatEndSystem               // Phase 4: Cleanup
```

### New Events
```csharp
ActionQueuedEventData
ActionStartedEventData
ActionResolvedEventData
ActionFailedEventData
ConditionAppliedEventData
ConditionExpiredEventData
ResourceConsumedEventData
TurnStartedEventData
RoundStartedEventData
```

---

## Implementation Steps

### 1. Define Action Types ✓
- Attack, CastSpell, Move, Dodge, Use Item, Pass

### 2. Action System Components
- ActionQueueComponent (queue of 3-5 actions)
- ActionComponent (type, cost, target, effects)
- ActionCostComponent (actions: 1-3, bonus: 0-1, reactions: 0-1)

### 3. Turn Management
- TurnQueueComponent (ordered list of entities)
- Execute turns in order
- Track whose turn it is

### 4. Combat Phases
- Phase 0: Initialize (initiative rolls)
- Phase 1a: Action selection 
- Phase 1b: Action resolution
- Phase 2: Turn transition
- Phase 3: Round transition  
- Phase 4: Combat end

### 5. Events & Logging
- Emit action events for all state changes
- Log to EventLog for replay
- Subscribe systems to react to events

---

## Success Criteria

- [ ] Action queue system working
- [ ] Multiple actions per turn supported
- [ ] Turn order respected
- [ ] Round transitions smooth
- [ ] All actions emit events
- [ ] Conditions applied correctly
- [ ] Combat phases execute in order
- [ ] Determinism maintained
- [ ] 30+ new tests passing
- [ ] No breaking changes to existing code

---

## File Manifest

**New Files:**
- `CombatActions.cs` - Action definitions
- `ActionSystem.cs` - Action processing
- `TurnManagementSystem.cs` - Turn queue management
- `CombatPhaseSystem.cs` - Phase transitions
- `AdvancedCombatTests.cs` - 30+ tests

**Modified Files:**
- `CombatComponent.cs` - Add action economy fields
- `CombatSystem.cs` - Integrate new systems
- `CombatEvents.cs` - Add new event types
- `EventBus.cs` - (no changes needed)

---

## Estimated Effort

- Components & events: 2 hours
- Action system: 3 hours
- Turn management: 2 hours
- Phase system: 2 hours
- Tests: 2 hours
- **Total: ~11 hours**

---

## Ready to Start?

This plan leverages:
✅ Data-oriented event system (already built)
✅ Existing combat components
✅ Seeded deterministic RNG
✅ 77+ passing tests

Let's build it! 🚀
