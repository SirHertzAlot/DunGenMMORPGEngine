using Unity.Entities;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Simulation.RNG;
using DunGen.ECS.Combat;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using Unity.Collections;

namespace DunGen.ECS.Systems.Combat
{
    /// <summary>
    /// Resolves queued actions in deterministic order.
    /// Handles action validation, execution, and event emission.
    /// </summary>
    public partial class ActionResolutionSystem : SystemBase
    {
        private EventBus _eventBus;
        private DeterministicRNG _rng;
        private EntityIndexCache _entityCache;

        public ActionResolutionSystem()
        {
            _rng = new DeterministicRNG();
            _entityCache = EntityIndexCache.Instance;
        }

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            // Process each combatant's queued actions
            Entities
                .WithoutBurst()
                .ForEach((Entity entity, ref CombatComponent combat, ref ActionQueueComponent actions,
                         ref ActionCostComponent costs, ref CombatStatsComponent stats) =>
                {
                    if (!combat.IsInCombat || combat.IsDead)
                        return;

                    // Get next action
                    var action = actions.GetNextAction();
                    if (action.Type == 0 && actions.ExecutedActionCount >= actions.QueuedActionCount)
                        return;  // No more actions

                    // Validate action can be executed
                    if (!costs.CanAfford(action.ActionCost))
                    {
                        // Emit failure event
                        _eventBus?.Publish(new ActionFailedEventData
                        {
                            EventId = _eventBus.GetNextEventId(),
                            FrameNumber = (uint)UnityEngine.Time.frameCount,
                            Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                            ActorEntityId = entity.Index,
                            ActionType = (int)action.Type,
                            TargetEntityId = action.TargetEntityId,
                            FailureReason = "Insufficient action resources"
                        });
                        return;
                    }

                    // Emit action started
                    _eventBus?.Publish(new ActionStartedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        ActorEntityId = entity.Index,
                        ActionType = (int)action.Type,
                        TargetEntityId = action.TargetEntityId,
                        ActionName = action.Name.ToString()
                    });

                    // Execute action based on type
                    var result = ResolveAction(entity, ref action, ref combat, ref costs, ref stats);

                    // Spend resources
                    costs.SpendAction(action.ActionCost);

                    // Emit action resolved
                    _eventBus?.Publish(new ActionResolvedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        ActorEntityId = entity.Index,
                        ActionType = (int)action.Type,
                        TargetEntityId = action.TargetEntityId,
                        IsSuccessful = result.IsSuccessful,
                        EffectValue = result.EffectValue
                    });

                    // Advance to next action
                    actions.AdvanceAction();

                }).Run();
        }

        private (bool IsSuccessful, int EffectValue) ResolveAction(
            Entity entity,
            ref CombatAction action,
            ref CombatComponent combat,
            ref ActionCostComponent costs,
            ref CombatStatsComponent stats)
        {
            return action.Type switch
            {
                DunGen.ECS.Combat.ActionType.Attack => ResolveAttack(entity, ref action, ref stats),
                DunGen.ECS.Combat.ActionType.Dodge => ResolveDodge(entity),
                DunGen.ECS.Combat.ActionType.Move => ResolveMove(entity, ref action, ref costs),
                DunGen.ECS.Combat.ActionType.CastSpell => ResolveCastSpell(entity, ref action, ref stats),
                DunGen.ECS.Combat.ActionType.UseItem => ResolveUseItem(entity, ref combat, ref stats),
                _ => (true, 0)
            };
        }

        private (bool IsSuccessful, int EffectValue) ResolveAttack(
            Entity attackerEntity, ref CombatAction action, ref CombatStatsComponent stats)
        {
            var targetEntity = FindEntityByIndex(action.TargetEntityId);
            if (targetEntity == Entity.Null || !EntityManager.HasComponent<CombatComponent>(targetEntity))
                return (false, 0);

            var targetCombat = EntityManager.GetComponentData<CombatComponent>(targetEntity);
            // D20 attack roll
            int d20 = _rng.RollD20();
            int attackModifier = stats.StrengthModifier + stats.ProficiencyBonus;
            int targetAc = targetCombat.ArmorClass;
            if (EntityManager.HasComponent<ConditionComponent>(targetEntity))
            {
                var conditions = EntityManager.GetComponentData<ConditionComponent>(targetEntity);
                if (conditions.HasShield)
                    targetAc += 2;
            }

            bool isCritical = d20 == 20;
            bool isFumble = d20 == 1;
            int attackRoll = d20 + attackModifier;
            bool isHit = isCritical || (!isFumble && attackRoll >= targetAc);

            // If hit, roll damage (1d8 + STR)
            int damage = 0;
            if (isHit)
            {
                damage = _rng.RollDice(8) + stats.StrengthModifier;
                if (isCritical)
                    damage *= 2;

                float multiplier = 1.0f;
                if (EntityManager.HasComponent<DamageProfileComponent>(targetEntity))
                {
                    multiplier = EntityManager.GetComponentData<DamageProfileComponent>(targetEntity).GetDamageMultiplier("Physical");
                }

                int adjustedDamage = System.Math.Max(1, (int)(damage * multiplier));
                targetCombat.CurrentHealth = System.Math.Max(0, targetCombat.CurrentHealth - adjustedDamage);
                EntityManager.SetComponentData(targetEntity, targetCombat);

                _eventBus?.Publish(new DamageInflictedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    VictimEntityId = targetEntity.Index,
                    DamageDealt = adjustedDamage,
                    DamageType = "Physical",
                    DamageMultiplier = multiplier,
                    BaseDamage = damage,
                    DamageSource = action.Name.ToString(),
                    VictimHealthRemaining = targetCombat.CurrentHealth
                });

                damage = adjustedDamage;

                if (targetCombat.IsDead)
                {
                    targetCombat.IsInCombat = false;
                    EntityManager.SetComponentData(targetEntity, targetCombat);
                    _eventBus?.Publish(new DeathEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        DeceasedEntityId = targetEntity.Index,
                        KillerEntityId = attackerEntity.Index,
                        SurvivingCombatants = System.Array.Empty<int>(),
                        CauseOfDeath = action.Name.ToString()
                    });
                }
            }

            _eventBus?.Publish(new AttackResolvedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                AttackerEntityId = attackerEntity.Index,
                DefenderEntityId = targetEntity.Index,
                D20Roll = d20,
                AttackModifier = attackModifier,
                TargetAC = targetAc,
                FinalAttackRoll = attackRoll,
                IsHit = isHit,
                IsNaturalTwenty = isCritical,
                IsNaturalOne = isFumble,
                WeaponName = action.Name.ToString(),
                DamageIfHit = damage
            });

            return (isHit, damage);
        }

        private (bool IsSuccessful, int EffectValue) ResolveDodge(ref CombatComponent combat)
            => (true, 0);

        private (bool IsSuccessful, int EffectValue) ResolveDodge(Entity entity)
        {
            if (!EntityManager.HasComponent<ConditionComponent>(entity))
                return (true, 1);

            var conditions = EntityManager.GetComponentData<ConditionComponent>(entity);
            if (!conditions.HasShield)
            {
                conditions.HasShield = true;
                conditions.ActiveConditionCount++;
                EntityManager.SetComponentData(entity, conditions);
            }

            _eventBus?.Publish(new ConditionAppliedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                TargetEntityId = entity.Index,
                ConditionName = "Shield",
                DurationFrames = 1,
                SourceEntityId = entity.Index
            });

            return (true, 1);
        }

        private (bool IsSuccessful, int EffectValue) ResolveMove(Entity entity, ref CombatAction action, ref ActionCostComponent costs)
        {
            if (!EntityManager.HasComponent<PositionComponent>(entity) || !EntityManager.HasComponent<MovementComponent>(entity))
                return (false, 0);

            var position = EntityManager.GetComponentData<PositionComponent>(entity);
            var movement = EntityManager.GetComponentData<MovementComponent>(entity);
            int originalX = position.X;
            int originalY = position.Y;

            if (movement.TilesMovedThisTurn >= movement.MovementSpeed || costs.MovementRemaining <= 0)
                return (false, 0);

            if (TryGetPosition(action.TargetEntityId, out var targetPosition))
            {
                position.X += System.Math.Sign(targetPosition.X - position.X);
                position.Y += System.Math.Sign(targetPosition.Y - position.Y);
            }
            else
            {
                position.X += 1;
            }

            movement.TilesMovedThisTurn++;
            costs.MovementRemaining = System.Math.Max(0, costs.MovementRemaining - 5);

            EntityManager.SetComponentData(entity, position);
            EntityManager.SetComponentData(entity, movement);

            _eventBus?.Publish(new EntityMovedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                SourceEntity = entity,
                FromX = originalX,
                FromY = originalY,
                ToX = position.X,
                ToY = position.Y
            });

            return (true, System.Math.Abs(position.X - originalX) + System.Math.Abs(position.Y - originalY));
        }

        private (bool IsSuccessful, int EffectValue) ResolveCastSpell(
            Entity casterEntity, ref CombatAction action, ref CombatStatsComponent stats)
        {
            if (stats.CurrentMana < 5)  // Assume spell costs 5 mana
                return (false, 0);

            // Roll spell attack (1d20 + INT)
            int d20 = _rng.RollD20();
            int spellRoll = d20 + stats.IntelligenceModifier;
            
            stats.CurrentMana -= 5;

            _eventBus?.Publish(new ResourceConsumedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                ActorEntityId = casterEntity.Index,
                ResourceType = "Mana",
                AmountConsumed = 5,
                RemainingAmount = stats.CurrentMana
            });

            _eventBus?.Publish(new SpellCastEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)UnityEngine.Time.frameCount,
                Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                CasterEntityId = casterEntity.Index,
                TargetEntityIds = action.TargetEntityId > 0 ? new[] { action.TargetEntityId } : System.Array.Empty<int>(),
                SpellName = action.Name.ToString(),
                ManaCost = 5,
                CasterManaRemaining = stats.CurrentMana
            });

            int damage = _rng.RollDice(6) + stats.IntelligenceModifier;
            if (spellRoll >= 10)
            {
                var targetEntity = FindEntityByIndex(action.TargetEntityId);
                if (targetEntity != Entity.Null && EntityManager.HasComponent<CombatComponent>(targetEntity))
                {
                    var targetCombat = EntityManager.GetComponentData<CombatComponent>(targetEntity);
                    targetCombat.CurrentHealth = System.Math.Max(0, targetCombat.CurrentHealth - damage);
                    EntityManager.SetComponentData(targetEntity, targetCombat);

                    _eventBus?.Publish(new DamageInflictedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        VictimEntityId = targetEntity.Index,
                        DamageDealt = damage,
                        DamageType = "Arcane",
                        DamageMultiplier = 1.0f,
                        BaseDamage = damage,
                        DamageSource = action.Name.ToString(),
                        VictimHealthRemaining = targetCombat.CurrentHealth
                    });

                    if (targetCombat.IsDead)
                    {
                        targetCombat.IsInCombat = false;
                        EntityManager.SetComponentData(targetEntity, targetCombat);
                        _eventBus?.Publish(new DeathEventData
                        {
                            EventId = _eventBus.GetNextEventId(),
                            FrameNumber = (uint)UnityEngine.Time.frameCount,
                            Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                            DeceasedEntityId = targetEntity.Index,
                            KillerEntityId = casterEntity.Index,
                            SurvivingCombatants = System.Array.Empty<int>(),
                            CauseOfDeath = action.Name.ToString()
                        });
                    }
                }
            }

            return (spellRoll >= 10, damage);
        }

        private (bool IsSuccessful, int EffectValue) ResolveUseItem(ref CombatAction action)
            => (true, 0);

        private (bool IsSuccessful, int EffectValue) ResolveUseItem(
            Entity entity, ref CombatComponent combat, ref CombatStatsComponent stats)
        {
            if (combat.CurrentHealth < combat.MaxHealth)
            {
                int healAmount = System.Math.Min(10, combat.MaxHealth - combat.CurrentHealth);
                combat.CurrentHealth += healAmount;
                _eventBus?.Publish(new ItemUsedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    UserEntityId = entity.Index,
                    ItemName = "Healing Potion",
                    QuantityUsed = 1,
                    QuantityRemaining = 0
                });
                _eventBus?.Publish(new HealingReceivedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    RecipientEntityId = entity.Index,
                    HealingAmount = healAmount,
                    HealingSource = "Healing Potion",
                    RecipientHealthRemaining = combat.CurrentHealth,
                    OverhealingWasted = 0
                });
                return (true, healAmount);
            }

            if (stats.CurrentMana < stats.MaxMana)
            {
                int manaAmount = System.Math.Min(5, stats.MaxMana - stats.CurrentMana);
                stats.CurrentMana += manaAmount;
                _eventBus?.Publish(new ItemUsedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    UserEntityId = entity.Index,
                    ItemName = "Mana Potion",
                    QuantityUsed = 1,
                    QuantityRemaining = 0
                });
                _eventBus?.Publish(new ResourceConsumedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)UnityEngine.Time.frameCount,
                    Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                    ActorEntityId = entity.Index,
                    ResourceType = "ManaRestore",
                    AmountConsumed = -manaAmount,
                    RemainingAmount = stats.CurrentMana
                });
                return (true, manaAmount);
            }

            return (false, 0);
        }

        private bool TryGetPosition(int entityId, out PositionComponent position)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            if (_entityCache.TryGetEntity(entityId, out var targetEntity) &&
                EntityManager.HasComponent<PositionComponent>(targetEntity))
            {
                position = EntityManager.GetComponentData<PositionComponent>(targetEntity);
                return true;
            }

            position = default;
            return false;
        }

        private Entity FindEntityByIndex(int entityId)
        {
            return _entityCache.TryGetEntity(entityId, out var entity) ? entity : Entity.Null;
        }
    }

    /// <summary>
    /// Manages turn transitions and resets action economy each turn.
    /// </summary>
    public partial class TurnTransitionSystem : SystemBase
    {
        private EventBus _eventBus;
        private EntityIndexCache _entityCache;

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
            _entityCache = EntityIndexCache.Instance;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            Entities
                .WithoutBurst()
                .ForEach((ref TurnQueueComponent turnQueue, ref CombatRoundComponent round) =>
                {
                    if (turnQueue.CurrentTurnIndex >= turnQueue.TotalCombatants || turnQueue.TotalCombatants == 0)
                        return;

                    var previousActorId = turnQueue.GetCurrentActor();
                    if (!ShouldAdvanceTurn(previousActorId))
                        return;

                    turnQueue.AdvanceTurn();
                    round.CurrentTurnIndex = turnQueue.CurrentTurnIndex;

                    if (turnQueue.CurrentTurnIndex >= turnQueue.TotalCombatants)
                        return;

                    var nextActorId = turnQueue.GetCurrentActor();
                    PrepareCombatantForTurn(nextActorId);

                    _eventBus?.Publish(new TurnTransitionEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        PreviousActorId = previousActorId,
                        NextActorId = nextActorId,
                        RoundNumber = round.RoundNumber,
                        TurnNumber = turnQueue.CurrentTurnIndex
                    });

                }).Run();
        }

        private bool ShouldAdvanceTurn(int actorId)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            if (!_entityCache.TryGetEntity(actorId, out var entity))
                return true;

            if (EntityManager.HasComponent<ActionQueueComponent>(entity))
            {
                var queue = EntityManager.GetComponentData<ActionQueueComponent>(entity);
                if (queue.ExecutedActionCount < queue.QueuedActionCount)
                    return false;
            }

            if (EntityManager.HasComponent<ActionCostComponent>(entity))
            {
                var costs = EntityManager.GetComponentData<ActionCostComponent>(entity);
                if (costs.TotalActions > 0)
                    return false;
            }

            return true;
        }

        private void PrepareCombatantForTurn(int actorId)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            if (!_entityCache.TryGetEntity(actorId, out var entity))
                return;

            if (EntityManager.HasComponent<ActionCostComponent>(entity))
            {
                var costs = EntityManager.GetComponentData<ActionCostComponent>(entity);
                costs.ResetForNewTurn();
                EntityManager.SetComponentData(entity, costs);
            }

            if (EntityManager.HasComponent<ConditionComponent>(entity))
            {
                var conditions = EntityManager.GetComponentData<ConditionComponent>(entity);
                ExpireTurnScopedConditions(ref conditions);
                EntityManager.SetComponentData(entity, conditions);
            }

            if (EntityManager.HasComponent<CombatComponent>(entity))
            {
                var combat = EntityManager.GetComponentData<CombatComponent>(entity);
                combat.HasActedThisRound = false;
                EntityManager.SetComponentData(entity, combat);
            }
        }

        private static void ExpireTurnScopedConditions(ref ConditionComponent conditions)
        {
            if (conditions.HasShield)
            {
                conditions.HasShield = false;
                if (conditions.ActiveConditionCount > 0)
                    conditions.ActiveConditionCount--;
            }

            if (conditions.IsProne && conditions.ProneDuration > 0)
            {
                conditions.ProneDuration--;
                if (conditions.ProneDuration == 0)
                {
                    conditions.IsProne = false;
                    if (conditions.ActiveConditionCount > 0)
                        conditions.ActiveConditionCount--;
                }
            }
        }
    }

    /// <summary>
    /// Manages round transitions when all combatants have acted.
    /// </summary>
    public partial class RoundTransitionSystem : SystemBase
    {
        private EventBus _eventBus;

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            Entities
                .WithoutBurst()
                .ForEach((ref TurnQueueComponent turnQueue, ref CombatRoundComponent round) =>
                {
                    if (!turnQueue.IsRoundComplete())
                        return;

                    // Round is complete
                    round.RoundNumber++;
                    turnQueue.ResetForNewRound();

                    // Emit round transition event
                    _eventBus?.Publish(new RoundTransitionEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        CompletedRoundNumber = round.RoundNumber - 1,
                        NextRoundNumber = round.RoundNumber,
                        TotalDamageThisRound = round.DamageThisRound,
                        ActionsExecuted = round.ActionsThisRound
                    });

                }).Run();
        }
    }
}
