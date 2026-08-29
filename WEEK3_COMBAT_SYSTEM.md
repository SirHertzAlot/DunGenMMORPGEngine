# Week 3: Combat System Implementation Plan

> Historical planning snapshot: this document reflects the original Week 3 plan.
> For current validated implementation status and priorities, see `OBJECTIVES_REALIGNMENT_PLAN.md` and `README_ALPHA.md`.

## Overview
Build a fully deterministic, D&D-inspired combat system with turn-based action queuing, attack resolution using d20 mechanics, and damage calculation driven by character stats and equipment.

**Duration:** 1 week (5 working days)  
**Build on:** Week 1-2 Foundation (deterministic RNG, ECS, events)  
**Output:** Playable combat scenarios with 15+ test cases  

---

## Goals

- ✅ Implement complete attack resolution (d20 + modifiers vs AC)
- ✅ Implement damage calculation (weapon dice + stat modifiers)
- ✅ Implement turn-based combat queue with initiative
- ✅ Integrate combat into ECS with CombatSystem
- ✅ Create combat-specific events (AttackEvent, DamageEvent, DeathEvent)
- ✅ Write 15+ deterministic combat test scenarios
- ✅ Validate balance metrics (DPS, TTK, survivability)
- ✅ Maintain 100% determinism across all combats

---

## Core Mechanics

### 1. Initiative & Turn Order

```
Initiative = d20 + DEX modifier
- Roll once per combat start using seeded RNG
- Sort participants by initiative descending
- Roll d20 ties using LCG (deterministic)
- Queue action in strict order each turn
```

**Implementation:**
- Add `InitiativeQueue` component to track turn order
- Add `IsInCombat` flag to enable CombatSystem processing
- Create `InitiativeRollEvent` when combat starts

---

### 2. Attack Resolution (D&D 5e-inspired)

```
Attack Roll = d20 + Attack Modifier (STR or DEX) vs Target AC
- d20 roll (LCG seeded RNG)
- Add STR modifier for melee, DEX for ranged
- Roll is success if result >= target AC
- Natural 20 always hits, natural 1 always misses
```

**Modifiers:**
- Melee: STR modifier from character stats
- Ranged: DEX modifier from character stats  
- Equipment: Magical weapons add +1, +2, or +3

**Implementation:**
- `AttackResolver` system processes attack requests
- Uses RNG with current seed for d20 roll
- Stores roll result in `AttackRollResult` component
- Fires `AttackResolvedEvent` with hit/miss data

---

### 3. Damage Calculation

```
Damage = Weapon Dice + STR/DEX Modifier + Spell Power
Example:
- Longsword: 1d8 + STR modifier
- Dagger: 1d4 + DEX modifier
- Fireball: 8d6 (no ability modifier)
```

**Damage Types:**
- Physical (melee/ranged weapons)
- Magical (spells, enchanted effects)
- Healing (reversible damage reduction)

**Resistances/Vulnerabilities:**
- Creatures can have damage resistance (half damage)
- Creatures can have vulnerability (double damage)
- Stored in `DamageProfile` component

**Implementation:**
- `DamageCalculator` computes damage from weapon/spell
- Applies modifier from attacking character
- Applies resistances/vulnerabilities
- Stores final damage in `DamageEvent`

---

### 4. Turn-Based Combat Queue

```
Combat Flow Each Turn:
1. Process actions in initiative order
2. Each combatant takes one action (attack, cast spell, move, etc.)
3. After all act, increment turn counter
4. Check victory conditions (all enemies dead, etc.)
5. Repeat until combat ends
```

**Action Types:**
- Attack (melee or ranged)
- Cast Spell (consumes mana)
- Move (change position)
- Use Item (from inventory)
- Defend (bonus AC until next turn)

**Implementation:**
- `CombatTurn` component tracks current actor
- `ActionQueue` holds pending actions
- CombatSystem processes one action per turn from queue
- Fires events for each action type

---

### 5. Combat Events

All combat events inherit from `GameEvent` and are logged deterministically:

```csharp
public class InitiativeRolledEvent : GameEvent
{
    public int ActorEntityId;
    public int Initiative;
    public int D20Roll;
    public int DexModifier;
}

public class AttackResolvedEvent : GameEvent
{
    public int AttackerEntityId;
    public int DefenderEntityId;
    public int D20Roll;
    public int AttackModifier;
    public int TargetAC;
    public bool IsHit;
    public int Damage; // 0 if miss
}

public class DamageInflictedEvent : GameEvent
{
    public int VictimEntityId;
    public int DamageAmount;
    public string DamageType;
    public int HealthRemaining;
}

public class CombatStartedEvent : GameEvent
{
    public List<int> ParticipantIds;
    public int[] InitiativeOrder;
}

public class CombatEndedEvent : GameEvent
{
    public int[] Survivors;
    public int[] Defeated;
}

public class DeathEvent : GameEvent
{
    public int DeceasedEntityId;
    public int KillerEntityId;
}
```

---

## Implementation Files

### New Source Files

1. **`CombatComponents.cs`** (120 lines)
   - `CombatComponent` — tracks health, AC, combat state
   - `InitiativeQueueComponent` — turn order tracking
   - `ActionQueueComponent` — pending actions
   - `CombatStatsComponent` — attack/damage modifiers
   - `DamageProfileComponent` — resistances/vulnerabilities

2. **`CombatSystem.cs`** (200 lines)
   - `CombatSystem` ECS system — processes combat logic each frame
   - `InitiativeCalculator` — d20 rolls for initiative
   - `AttackResolver` — d20 attack resolution
   - `DamageCalculator` — weapon damage computation
   - `ActionProcessor` — executes queued actions

3. **`CombatEvents.cs`** (80 lines)
   - All combat event types (listed above)
   - Event fire helpers

4. **`CombatHelpers.cs`** (100 lines)
   - `CombatValidator` — ensures valid state transitions
   - `BalanceMetricsCalculator` — computes DPS, TTK, etc.
   - `CombatScenarioBuilder` — creates test scenarios

### Modified Files

1. **`CoreComponents.cs`**
   - Merge `CombatComponent` and `CombatStatsComponent` into expanded component set

2. **`GameEvent.cs`**
   - Add combat event types to base event system

3. **`Simulation.cs`**
   - Add CombatSystem to simulation tick processing

### Test Files

1. **`CombatSystemTests.cs`** (350 lines)
   - 15+ test scenarios covering:
     - Initiative rolling and determinism
     - Attack resolution (hits, misses, critical hits)
     - Damage calculation with modifiers
     - Multi-round combat sequences
     - Healing mechanics
     - Resistances and vulnerabilities
     - Victory conditions
     - Balance metrics validation

---

## Test Scenarios (15+)

### Initiative Tests
- [ ] `InitiativeRoll_IsDeterministic` — Same seed → same initiative order
- [ ] `InitiativeRoll_BreaksTies_Deterministically` — d20 ties resolved consistently
- [ ] `InitiativeOrder_SortsDescending` — Highest initiative goes first

### Attack Resolution Tests
- [ ] `AttackHit_WhenRollMeetsOrExceedsAC` — 10 + 2 = 12 vs AC 12 hits
- [ ] `AttackMiss_WhenRollBelowAC` — 5 + 2 = 7 vs AC 10 misses
- [ ] `AttackNaturalTwenty_AlwaysHits` — d20= 20 always hits regardless of AC
- [ ] `AttackNaturalOne_AlwaysMisses` — d20 = 1 always misses regardless of AC
- [ ] `MeleeAttack_UsesStrModifier` — Longsword attack uses STR not DEX
- [ ] `RangedAttack_UsesDexModifier` — Bow attack uses DEX not STR

### Damage Calculation Tests
- [ ] `DamageRoll_IsDeterministic` — Same seed → same damage
- [ ] `DamageIncludesModifier` — Sword damage = d8 + STR mod
- [ ] `DamageRoll_RespectsDiceNotation` — 2d6 produces 2-12 range
- [ ] `DamageResistance_HalvesDamage` — 10 damage with resistance = 5
- [ ] `DamageVulnerability_DoublesDamage` — 10 damage with vuln = 20

### Multi-Round Combat Tests
- [ ] `SimpleDuel_Deterministic` — Two fighters exchange attacks deterministically
- [ ] `MultiRound_ConsistentOutcome` — 3+ round combat always same victor
- [ ] `FullCombatSession_ReplayableFromEventLog` — Replay events produce same state

### Balance Tests
- [ ] `AverageDPS_WithinTarget` — Typical combatant deals expected damage
- [ ] `TimeToKill_Reasonable` — Average fight lasts 2-5 rounds

---

## Success Criteria

- ✅ All 15+ combat tests pass
- ✅ 100% determinism across all combat sequences
- ✅ Combat DPS metrics within 10% of intended balance
- ✅ Time-to-kill ranges 2-5 rounds for typical matchups
- ✅ Attack hit-rate realistic (50-70% at equivalent levels)
- ✅ Event log captures every attack/damage/death
- ✅ Code coverage remains ≥70%
- ✅ Zero technical debt

---

## Implementation Sequence

**Day 1:**
- Create CombatComponents.cs with all component types
- Create CombatSystem.cs skeleton with basic processing
- Export `CombatEvents.cs` with event types

**Day 2:**
- Implement AttackResolver with d20 mechanics
- Implement DamageCalculator with dice rolls
- Create InitiativeCalculator for turn order

**Day 3:**
- Build ActionQueue and CombatTurn logic
- Implement CombatSystem main loop
- Integrate into Simulation.cs

**Day 4:**
- Write all 15+ combat test scenarios
- Debug and balance as needed
- Validate determinism across seed variations

**Day 5:**
- Polish and documentation
- Final balance metrics
- Prepare for Week 4 (Procedural Generation)

---

## Dependencies

**From Week 1:**
- RNG (deterministic seeding)
- EventBus (combat event pub-sub)
- EventLog (replay capability)
- ECS infrastructure (World, Entities, Systems)
- GameEvent base class

**External:**
- Existing character stats configuration (YAML)
- Existing weapon/spell definitions (YAML)

---

## Deliverables

1. ✅ 4 new C# source files (CombatComponents, CombatSystem, CombatEvents, CombatHelpers)
2. ✅ 1 comprehensive test file (CombatSystemTests.cs with 15+ tests)
3. ✅ 100% determinism validation
4. ✅ Balance metrics report
5. ✅ Updated documentation for combat mechanics
6. ✅ Ready for Week 4 procedural generation

---

## Notes

- All combat must be deterministic and replayable
- All random rolls use the seeded RNG from Week 1  
- All combat events must be logged to enable replay
- Balance must favor engagement (2-5 round fights, not 1-shot kills)
- Architecture must support both turn-based testing and eventual real-time combat
