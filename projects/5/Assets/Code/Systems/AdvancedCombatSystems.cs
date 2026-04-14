using Unity.Entities;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Simulation.RNG;
using DunGen.ECS.Core;
using DunGen.ECS.Combat;

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

        public ActionResolutionSystem()
        {
            _rng = new DeterministicRNG();
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
                .ForEach((ref CombatComponent combat, ref ActionQueueComponent actions, 
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
                            ActorEntityId = combat.CombatSessionId,
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
                        ActorEntityId = combat.CombatSessionId,
                        ActionType = (int)action.Type,
                        TargetEntityId = action.TargetEntityId,
                        ActionName = action.Name
                    });

                    // Execute action based on type
                    var result = ResolveAction(ref action, ref combat, ref stats);

                    // Spend resources
                    costs.SpendAction(action.ActionCost);

                    // Emit action resolved
                    _eventBus?.Publish(new ActionResolvedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        ActorEntityId = combat.CombatSessionId,
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
            ref CombatAction action, ref CombatComponent combat, ref CombatStatsComponent stats)
        {
            return action.Type switch
            {
                DunGen.ECS.Combat.ActionType.Attack => ResolveAttack(ref action, ref stats),
                DunGen.ECS.Combat.ActionType.Dodge => ResolveDodge(ref combat),
                DunGen.ECS.Combat.ActionType.Move => ResolveMove(ref action),
                DunGen.ECS.Combat.ActionType.CastSpell => ResolveCastSpell(ref action, ref stats),
                DunGen.ECS.Combat.ActionType.UseItem => ResolveUseItem(ref action),
                _ => (true, 0)
            };
        }

        private (bool IsSuccessful, int EffectValue) ResolveAttack(
            ref CombatAction action, ref CombatStatsComponent stats)
        {
            // D20 attack roll
            int d20 = _rng.RollD20();
            int attackRoll = d20 + stats.StrengthModifier;
            
            // Assume AC 12 for now
            bool isHit = attackRoll >= 12;
            
            // If hit, roll damage (1d8 + STR)
            int damage = isHit ? _rng.RollDice(8) + stats.StrengthModifier : 0;
            
            return (isHit, damage);
        }

        private (bool IsSuccessful, int EffectValue) ResolveDodge(ref CombatComponent combat)
        {
            // Add shield for next attack
            return (true, 0);
        }

        private (bool IsSuccessful, int EffectValue) ResolveMove(ref CombatAction action)
        {
            // Movement logic
            return (true, 0);
        }

        private (bool IsSuccessful, int EffectValue) ResolveCastSpell(
            ref CombatAction action, ref CombatStatsComponent stats)
        {
            if (stats.CurrentMana < 5)  // Assume spell costs 5 mana
                return (false, 0);

            // Roll spell attack (1d20 + INT)
            int d20 = _rng.RollD20();
            int spellRoll = d20 + stats.IntelligenceModifier;
            
            stats.CurrentMana -= 5;
            
            return (spellRoll >= 10, _rng.RollDice(6) + stats.IntelligenceModifier);
        }

        private (bool IsSuccessful, int EffectValue) ResolveUseItem(ref CombatAction action)
        {
            return (true, 0);
        }
    }

    /// <summary>
    /// Manages turn transitions and resets action economy each turn.
    /// </summary>
    public partial class TurnTransitionSystem : SystemBase
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
                .ForEach((ref TurnQueueComponent turnQueue, ref CombatRoundComponent round,
                         ref ActionCostComponent costs, ref ConditionComponent conditions) =>
                {
                    if (turnQueue.CurrentTurnIndex >= turnQueue.TotalCombatants)
                        return;

                    int previousActorId = turnQueue.GetCurrentActor();

                    // Transition to next turn
                    turnQueue.AdvanceTurn();
                    int nextActorId = turnQueue.GetCurrentActor();

                    // Emit turn transition event
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

                    // Reset action economy for the new actor
                    costs.ResetForNewTurn();

                    // Clear conditions that expired (decrement Prone duration and lift it when it runs out)
                    if (conditions.IsProne && conditions.ProneDuration > 0)
                    {
                        conditions.ProneDuration--;
                        if (conditions.ProneDuration == 0)
                            conditions.RemoveCondition("Prone");
                    }

                }).Run();
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
