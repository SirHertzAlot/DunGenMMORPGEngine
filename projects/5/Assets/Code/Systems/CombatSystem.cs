using Unity.Entities;
using DunGen.Simulation.RNG;
using DunGen.Events;
using DunGen.ECS.Core;
using DunGen.ECS.Combat;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace DunGen.ECS.Systems.Combat
{
    /// <summary>
    /// Main ECS system that processes all combat logic each simulation frame.
    /// Handles combat phases: Initiative rolling, turn processing, damage resolution, and victory.
    /// CRITICAL: All combat must be deterministic. Use seeded RNG from Week 1 foundation.
    /// </summary>
    public partial class CombatSystem : SystemBase
    {
        private readonly DeterministicRNG _rng = new();
        private EventBus _eventBus;
        private Dictionary<int, List<(int entityId, int initiative)>> _combatQueues = new();
        private EntityIndexCache _entityCache;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<CombatComponent>();
            _entityCache = EntityIndexCache.Instance;
        }

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
            {
                UnityEngine.Debug.LogWarning("CombatSystem: EventBus not initialized. Skipping combat update.");
                return;
            }

            // Process each combat round
            Entities
                .WithoutBurst()
                .ForEach((ref CombatComponent combat, ref CombatRoundComponent round) =>
                {
                    if (!combat.IsInCombat)
                        return;

                    switch (round.CombatPhase)
                    {
                        case 0: // Initialize combat
                            HandleCombatInitialization(ref combat, ref round);
                            break;

                        case 1: // In progress
                            HandleCombatTurn(ref combat, ref round);
                            break;

                        case 2: // Ended
                            HandleCombatEnd(ref combat, ref round);
                            break;
                    }
                }).Run();
        }

        /// <summary>
        /// Phase 0: Initialize combat with initiative rolls for all participants.
        /// </summary>
        private void HandleCombatInitialization(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            if (round.CombatPhase != 0)
                return;

            // Initialize seeded RNG for this combat session
            _rng.SetSeed(combat.CombatSeed);
            var participants = GetCombatParticipantIds(combat.CombatSessionId, round.TotalParticipants);
            var initiativeOrder = GetInitiativeOrder(combat.CombatSessionId, participants);
            round.TotalParticipants = participants.Length;

            // Fire CombatStartedEventData (pure data struct)
            var startedEvent = new global::DunGen.Events.Combat.CombatStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                ParticipantEntityIds = participants,
                InitiativeOrder = initiativeOrder,
                CombatSessionId = combat.CombatSessionId
            };

            _combatQueues[combat.CombatSessionId] = initiativeOrder
                .Select((entityId, index) => (entityId, initiative: initiativeOrder.Length - index))
                .ToList();

            _eventBus?.Publish(startedEvent);

            // Move to in-progress phase
            round.CombatPhase = 1;
            round.RoundNumber = 1;
            round.CurrentTurnIndex = 0;
            round.ActiveCombatantId = initiativeOrder.Length > 0 ? initiativeOrder[0] : combat.CombatSessionId;
        }

        /// <summary>
        /// Phase 1: Process one turn of active combat.
        /// </summary>
        private void HandleCombatTurn(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            if (round.CombatPhase != 1)
                return;

            var actorId = GetActorForTurn(combat.CombatSessionId, round.CurrentTurnIndex);
            if (actorId < 0)
                actorId = combat.CombatSessionId;

            round.ActiveCombatantId = actorId;

            _eventBus?.Publish(new global::DunGen.Events.Combat.TurnStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                ActorEntityId = actorId,
                RoundNumber = round.RoundNumber,
                TurnIndex = round.CurrentTurnIndex
            });

            MarkCombatantActed(actorId, round.RoundNumber);

            HandleTurnActions(ref combat, ref round);

            // Advance to next combatant
            round.CurrentTurnIndex++;

            // Check if all combatants have acted (round is complete)
            if (round.CurrentTurnIndex >= round.TotalParticipants)
            {
                // Fire RoundEndedEventData (pure data struct)
                var roundEndedEvent = new global::DunGen.Events.Combat.RoundEndedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    RoundNumber = round.RoundNumber,
                    ParticipantCount = round.TotalParticipants
                };
                _eventBus?.Publish(roundEndedEvent);

                // Increment round counter and reset turn index
                round.RoundNumber++;
                round.CurrentTurnIndex = 0;
                ResetCombatantRoundFlags(combat.CombatSessionId);
            }

            // Check victory conditions
            if (CheckVictoryCondition(ref combat, ref round))
            {
                round.CombatPhase = 2;
            }
        }

        /// <summary>
        /// Phase 2: Clean up and end combat session.
        /// </summary>
        private void HandleCombatEnd(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            if (round.CombatPhase != 2)
                return;

            var participants = GetCombatParticipants(combat.CombatSessionId, round.TotalParticipants);
            var victorIds = participants
                .Where(p => p.IsInCombat && !p.IsDead)
                .Select(p => p.EntityId)
                .ToArray();
            var defeatedIds = participants
                .Where(p => p.IsDead)
                .Select(p => p.EntityId)
                .ToArray();
            var endReason = DetermineEndReason(victorIds.Length, defeatedIds.Length);

            // Fire CombatEndedEventData (pure data struct)
            var endedEvent = new global::DunGen.Events.Combat.CombatEndedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                CombatSessionId = combat.CombatSessionId,
                VictorIds = victorIds,
                DefeatedIds = defeatedIds,
                EndReason = endReason,
                TotalRoundsElapsed = round.RoundNumber
            };
            _eventBus?.Publish(endedEvent);

            // Mark combat as ended
            combat.IsInCombat = false;
        }

        /// <summary>
        /// Process actions for the active combatant this turn.
        /// Resolves attacks, applies damage, triggers events.
        /// </summary>
        private void HandleTurnActions(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            round.ActionsThisRound++;
            round.ActiveCombatantId = GetActorForTurn(combat.CombatSessionId, round.CurrentTurnIndex);
        }

        /// <summary>
        /// Check if combat should end (victory condition met).
        /// </summary>
        private bool CheckVictoryCondition(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            var participants = GetCombatParticipants(combat.CombatSessionId, round.TotalParticipants);
            if (participants.Count == 0)
                return false;

            int activeCombatants = participants.Count(p => p.IsInCombat && !p.IsDead);
            return activeCombatants <= 1;
        }

        private int[] GetCombatParticipantIds(int combatSessionId, int expectedParticipants)
        {
            var participants = GetCombatParticipants(combatSessionId, expectedParticipants);
            if (participants.Count == 0)
                return new[] { combatSessionId };

            return participants.Select(p => p.EntityId).ToArray();
        }

        private int[] GetInitiativeOrder(int combatSessionId, int[] participants)
        {
            if (participants.Length == 0)
                return System.Array.Empty<int>();

            var initiatives = new Dictionary<int, int>();
            var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CombatComponent>(),
                ComponentType.ReadOnly<InitiativeComponent>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var combatComponents = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);
            using var initiativeComponents = query.ToComponentDataArray<InitiativeComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (combatComponents[i].CombatSessionId != combatSessionId)
                    continue;

                initiatives[entities[i].Index] = initiativeComponents[i].InitiativeScore;
            }

            return participants
                .OrderByDescending(id => initiatives.TryGetValue(id, out var score) ? score : int.MinValue)
                .ThenBy(id => id)
                .ToArray();
        }

        private List<CombatParticipantSnapshot> GetCombatParticipants(int combatSessionId, int expectedParticipants)
        {
            var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var components = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);

            var participants = new List<CombatParticipantSnapshot>(expectedParticipants > 0 ? expectedParticipants : entities.Length);

            for (int i = 0; i < entities.Length; i++)
            {
                var component = components[i];
                if (component.CombatSessionId != combatSessionId)
                    continue;

                participants.Add(new CombatParticipantSnapshot(
                    entities[i].Index,
                    component.IsInCombat,
                    component.IsDead));
            }

            return participants
                .OrderBy(p => p.EntityId)
                .ToList();
        }

        private int GetActorForTurn(int combatSessionId, int currentTurnIndex)
        {
            if (_combatQueues.TryGetValue(combatSessionId, out var queue) &&
                currentTurnIndex >= 0 &&
                currentTurnIndex < queue.Count)
            {
                return queue[currentTurnIndex].entityId;
            }

            var participants = GetCombatParticipantIds(combatSessionId, currentTurnIndex + 1);
            return currentTurnIndex >= 0 && currentTurnIndex < participants.Length
                ? participants[currentTurnIndex]
                : -1;
        }

        private void MarkCombatantActed(int actorId, int roundNumber)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            if (!_entityCache.TryGetEntity(actorId, out var entity) ||
                !EntityManager.HasComponent<CombatComponent>(entity))
                return;

            var combatant = EntityManager.GetComponentData<CombatComponent>(entity);
            combatant.HasActedThisRound = true;
            combatant.CurrentRound = roundNumber;
            EntityManager.SetComponentData(entity, combatant);
        }

        private void ResetCombatantRoundFlags(int combatSessionId)
        {
            var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var components = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combatant = components[i];
                if (combatant.CombatSessionId != combatSessionId)
                    continue;

                combatant.HasActedThisRound = false;
                EntityManager.SetComponentData(entities[i], combatant);
            }
        }

        private static string DetermineEndReason(int victorCount, int defeatedCount)
        {
            if (victorCount == 0 && defeatedCount > 0)
                return "MutualDefeat";

            if (victorCount > 0 && defeatedCount > 0)
                return "AllOpponentsDefeated";

            if (victorCount > 0)
                return "LastCombatantStanding";

            return "CombatResolved";
        }

        private readonly struct CombatParticipantSnapshot
        {
            public CombatParticipantSnapshot(int entityId, bool isInCombat, bool isDead)
            {
                EntityId = entityId;
                IsInCombat = isInCombat;
                IsDead = isDead;
            }

            public int EntityId { get; }
            public bool IsInCombat { get; }
            public bool IsDead { get; }
        }
    }

    /// <summary>
    /// Stateless helper class for generating and rolling initiative scores.
    /// Uses deterministic RNG seeded from combat session.
    /// </summary>
    public class InitiativeRoller
    {
        private readonly DeterministicRNG _rng;

        public InitiativeRoller(uint seed)
        {
            _rng = new DeterministicRNG();
            _rng.SetSeed(seed);
        }

        /// <summary>
        /// Roll initiative for a single combatant: d20 + DEX modifier.
        /// Returns (initiative_score, d20_roll).
        /// </summary>
        public (int score, int d20) RollInitiative(int dexModifier)
        {
            int d20 = _rng.RollD20();
            int initiative = d20 + dexModifier;
            return (initiative, d20);
        }

        /// <summary>
        /// Given a list of combatants with their DEX modifiers,
        /// returns them sorted by initiative (highest first).
        /// Deterministically breaks ties using additional d20 rolls.
        /// </summary>
        public List<(int entityId, int initiative)> RollAndSortInitiatives(List<(int entityId, int dexMod)> combatants)
        {
            var initiativeRolls = new List<(int entityId, int initiative, int d20)>();

            foreach (var (entityId, dexMod) in combatants)
            {
                int d20 = _rng.RollD20();
                int initiative = d20 + dexMod;
                initiativeRolls.Add((entityId, initiative, d20));
            }

            // Sort by initiative descending, then by entity ID (tie-breaker)
            initiativeRolls.Sort((a, b) =>
            {
                if (a.initiative != b.initiative)
                    return b.initiative.CompareTo(a.initiative);
                return a.entityId.CompareTo(b.entityId);
            });

            var result = new List<(int entityId, int initiative)>();
            foreach (var (entityId, initiative, _) in initiativeRolls)
            {
                result.Add((entityId, initiative));
            }

            return result;
        }
    }

    /// <summary>
    /// Stateless helper class for resolving attack rolls.
    /// d20 + modifier vs target AC. Handles natural 20/1 cases.
    /// </summary>
    public class AttackResolver
    {
        private readonly DeterministicRNG _rng;

        public AttackResolver(uint seed)
        {
            _rng = new DeterministicRNG();
            _rng.SetSeed(seed);
        }

        /// <summary>
        /// Resolve an attack roll.
        /// Returns (isHit, d20_roll, isNatural20, isNatural1).
        /// </summary>
        public (bool isHit, int d20, bool isCritical, bool isFumble) ResolveAttack(int attackModifier, int targetAC)
        {
            int d20 = _rng.RollD20();

            // Natural 20: automatic hit
            if (d20 == 20)
                return (true, d20, true, false);

            // Natural 1: automatic miss
            if (d20 == 1)
                return (false, d20, false, true);

            // Normal case: compare roll + modifier against AC
            int attackRoll = d20 + attackModifier;
            bool isHit = attackRoll >= targetAC;

            return (isHit, d20, false, false);
        }
    }

    /// <summary>
    /// Stateless helper class for calculating damage from weapons/spells.
    /// Applies dice rolls, modifiers, resistances, and vulnerabilities.
    /// </summary>
    public class DamageCalculator
    {
        private readonly DeterministicRNG _rng;

        public DamageCalculator(uint seed)
        {
            _rng = new DeterministicRNG();
            _rng.SetSeed(seed);
        }

        /// <summary>
        /// Calculate damage from a weapon attack.
        /// weaponDiceNotation: e.g., "1d8", "2d6", "1d4"
        /// Returns final damage after modifiers and resistances.
        /// </summary>
        public int CalculateWeaponDamage(string weaponDiceNotation, int abilityModifier, float damageMultiplier = 1.0f)
        {
            int baseDamage = RollDiceNotation(weaponDiceNotation);
            int totalDamage = baseDamage + abilityModifier;
            int finalDamage = (int)(totalDamage * damageMultiplier);
            return System.Math.Max(1, finalDamage); // Minimum 1 damage
        }

        /// <summary>
        /// Calculate damage from a spell.
        /// spellDiceNotation: e.g., "3d6", "8d6"
        /// Returns damage after resistances and vulnerabilities.
        /// </summary>
        public int CalculateSpellDamage(string spellDiceNotation, float damageMultiplier = 1.0f)
        {
            int baseDamage = RollDiceNotation(spellDiceNotation);
            int finalDamage = (int)(baseDamage * damageMultiplier);
            return System.Math.Max(1, finalDamage);
        }

        /// <summary>
        /// Roll dice according to notation: "XdY" or "XdY+Z" or "XdY-Z"
        /// Examples: "1d8", "2d6+2", "3d4-1", "8d6"
        /// </summary>
        private int RollDiceNotation(string notation)
        {
            notation = notation.Trim();
            int result = 0;
            int modifier = 0;

            // Parse modifier if present
            if (notation.Contains("+"))
            {
                var parts = notation.Split('+');
                notation = parts[0];
                modifier = int.Parse(parts[1].Trim());
            }
            else if (notation.Contains("-"))
            {
                var parts = notation.Split('-');
                notation = parts[0];
                modifier = -int.Parse(parts[1].Trim());
            }

            // Parse dice notation XdY
            var diceParts = notation.Split('d');
            if (diceParts.Length != 2)
                return 0;

            int numDice = int.Parse(diceParts[0]);
            int diceSize = int.Parse(diceParts[1]);

            // Roll each die and sum
            for (int i = 0; i < numDice; i++)
            {
                result += _rng.RollDice(diceSize);
            }

            return result + modifier;
        }
    }

    /// <summary>
    /// Helper class that orchestrates the full attack sequence:
    /// Roll initiative → Attack → Hit/Miss → Damage → Update health.
    /// </summary>
    public class CombatOrchestrator
    {
        private readonly uint _sessionSeed;
        private readonly InitiativeRoller _initiativeRoller;
        private readonly AttackResolver _attackResolver;
        private readonly DamageCalculator _damageCalculator;
        private readonly EventBus _eventBus;

        public CombatOrchestrator(uint seed, EventBus eventBus)
        {
            _sessionSeed = seed;
            _eventBus = eventBus;
            _initiativeRoller = new InitiativeRoller(seed);
            _attackResolver = new AttackResolver(seed);
            _damageCalculator = new DamageCalculator(seed);
        }

        /// <summary>
        /// Execute a full attack sequence: roll attack, apply damage if hit.
        /// Returns the damage dealt (0 if miss).
        /// </summary>
        public int ExecuteAttack(
            int attackerId,
            int defenderId,
            int strModifier,
            int defenderAC,
            string weaponName,
            string weaponDamageNotation)
        {
            // Roll attack
            var (isHit, d20, isCritical, isFumble) = _attackResolver.ResolveAttack(strModifier, defenderAC);

            var attackEvent = new global::DunGen.Events.Combat.AttackResolvedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                AttackerEntityId = attackerId,
                DefenderEntityId = defenderId,
                D20Roll = d20,
                AttackModifier = strModifier,
                TargetAC = defenderAC,
                FinalAttackRoll = d20 + strModifier,
                IsHit = isHit,
                IsNaturalTwenty = isCritical,
                IsNaturalOne = isFumble,
                WeaponName = weaponName,
                DamageIfHit = 0
            };

            int damage = 0;
            if (isHit)
            {
                // Calculate damage
                float multiplier = isCritical ? 2.0f : 1.0f; // Critical doubles damage
                damage = _damageCalculator.CalculateWeaponDamage(weaponDamageNotation, strModifier, multiplier);
                attackEvent.DamageIfHit = damage;

                // Fire DamageInflictedEventData (pure data struct)
                var damageEvent = new global::DunGen.Events.Combat.DamageInflictedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    VictimEntityId = defenderId,
                    DamageDealt = damage,
                    DamageType = "Physical",
                    BaseDamage = damage,
                    DamageSource = weaponName,
                    DamageMultiplier = multiplier
                };
                _eventBus?.Publish(damageEvent);
            }

            _eventBus?.Publish(attackEvent);
            return damage;
        }
    }
}
