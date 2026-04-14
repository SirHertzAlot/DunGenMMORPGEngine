using Unity.Entities;
using DunGen.Simulation.RNG;
using DunGen.Events;
using DunGen.ECS.Core;
using DunGen.ECS.Combat;
using System.Collections.Generic;

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

        public override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<CombatComponent>();
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
                .ForEach((ref CombatComponent combat, ref CombatRoundComponent round,
                          ref TurnQueueComponent turnQueue) =>
                {
                    if (!combat.IsInCombat)
                        return;

                    switch (round.CombatPhase)
                    {
                        case 0: // Initialize combat
                            HandleCombatInitialization(ref combat, ref round, ref turnQueue);
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
        private void HandleCombatInitialization(ref CombatComponent combat, ref CombatRoundComponent round,
            ref TurnQueueComponent turnQueue)
        {
            if (round.CombatPhase != 0)
                return;

            // Initialize seeded RNG for this combat session
            _rng.SetSeed(combat.CombatSeed);

            // Build ordered participant and initiative arrays from the turn queue
            int count = turnQueue.TotalCombatants;
            var participantIds = new int[count];
            var initiativeOrder = new int[count];
            for (int i = 0; i < count; i++)
            {
                int id = turnQueue.GetCombatantAt(i);
                participantIds[i] = id;
                initiativeOrder[i] = id; // turn queue is already sorted by initiative
            }

            // Fire CombatStartedEventData (pure data struct)
            var startedEvent = new CombatStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                ParticipantEntityIds = participantIds,
                InitiativeOrder = initiativeOrder,
                CombatSessionId = combat.CombatSessionId
            };
            _eventBus?.Publish(startedEvent);

            // Move to in-progress phase
            round.CombatPhase = 1;
            round.RoundNumber = 1;
            round.CurrentTurnIndex = 0;
        }

        /// <summary>
        /// Phase 1: Process one turn of active combat.
        /// </summary>
        private void HandleCombatTurn(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            if (round.CombatPhase != 1)
                return;

            // Process current actor's turn
            // This will be expanded with action queue processing
            HandleTurnActions(ref combat, ref round);

            // Advance to next combatant
            round.CurrentTurnIndex++;

            // Check if all combatants have acted (round is complete)
            if (round.CurrentTurnIndex >= round.TotalParticipants)
            {
                // Fire RoundEndedEventData (pure data struct)
                var roundEndedEvent = new RoundEndedEventData
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

            // Fire CombatEndedEventData (pure data struct)
            var endedEvent = new CombatEndedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                CombatSessionId = combat.CombatSessionId,
                EndReason = combat.IsDead ? "Defeated" : "AllEnemiesDefeated",
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
            // Placeholder for action queue processing
            // Will be expanded in future updates
        }

        /// <summary>
        /// Check if combat should end (victory condition met).
        /// Returns true when this entity has been defeated (health at or below zero).
        /// </summary>
        private bool CheckVictoryCondition(ref CombatComponent combat, ref CombatRoundComponent round)
        {
            // Combat ends for this entity when they are defeated
            return combat.IsDead;
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

            // Fire AttackResolvedEvent
            var attackEvent = new AttackResolvedEvent
            {
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
                DamageIfHit = 0 // Will be populated if hit
            };

            int damage = 0;
            if (isHit)
            {
                // Calculate damage
                float multiplier = isCritical ? 2.0f : 1.0f; // Critical doubles damage
                damage = _damageCalculator.CalculateWeaponDamage(weaponDamageNotation, strModifier, multiplier);
                attackEvent.DamageIfHit = damage;

                // Fire DamageInflictedEventData (pure data struct)
                var damageEvent = new DamageInflictedEventData
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
