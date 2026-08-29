using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;

namespace DunGen.ECS.Combat
{
    /// <summary>
    /// Defines action types available in combat.
    /// </summary>
    public enum ActionType
    {
        Attack = 0,
        CastSpell = 1,
        Move = 2,
        Dodge = 3,
        UseItem = 4,
        Pass = 5
    }

    /// <summary>
    /// Single combat action with cost and effects.
    /// </summary>
    public struct CombatAction
    {
        public ActionType Type;
        public int TargetEntityId;
        public FixedString64Bytes Name;
        public int ActionCost;           // 0 = reaction, 1 = action, 2 = bonus action
        public int ManaCost;
        public FixedString128Bytes EffectDescription;
        public uint ExecutedAtFrame;
        public bool IsResolved;
    }

    /// <summary>
    /// Queue of pending actions for a combatant this turn.
    /// </summary>
    public struct ActionQueueComponent : IComponentData
    {
        // Use fixed array for determinism
        public const int MAX_QUEUED_ACTIONS = 5;
        
        public CombatAction Action0;
        public CombatAction Action1;
        public CombatAction Action2;
        public CombatAction Action3;
        public CombatAction Action4;
        
        public int QueuedActionCount;
        public int ExecutedActionCount;

        public void QueueAction(CombatAction action)
        {
            if (QueuedActionCount >= MAX_QUEUED_ACTIONS)
                return;

            switch (QueuedActionCount)
            {
                case 0: Action0 = action; break;
                case 1: Action1 = action; break;
                case 2: Action2 = action; break;
                case 3: Action3 = action; break;
                case 4: Action4 = action; break;
            }
            QueuedActionCount++;
        }

        public CombatAction GetNextAction()
        {
            if (ExecutedActionCount >= QueuedActionCount)
                return default;

            return ExecutedActionCount switch
            {
                0 => Action0,
                1 => Action1,
                2 => Action2,
                3 => Action3,
                4 => Action4,
                _ => default
            };
        }

        public void AdvanceAction()
        {
            if (ExecutedActionCount < QueuedActionCount)
                ExecutedActionCount++;
        }

        public void ClearQueue()
        {
            QueuedActionCount = 0;
            ExecutedActionCount = 0;
        }
    }

    /// <summary>
    /// Action economy - tracks resources available this turn.
    /// </summary>
    public struct ActionCostComponent : IComponentData
    {
        public int ActionsRemaining;      // Main actions (default 1)
        public int BonusActionsRemaining; // Bonus actions (default 0)
        public int ReactionsRemaining;   // Reactions (default 1)
        public int MovementRemaining;    // Movement points (in feet, default 30)

        public int TotalActions => ActionsRemaining + BonusActionsRemaining + ReactionsRemaining;

        public bool CanAfford(int actionCost)
        {
            if (actionCost == 0) return ReactionsRemaining > 0;
            if (actionCost == 1) return ActionsRemaining > 0;
            if (actionCost == 2) return BonusActionsRemaining > 0;
            return false;
        }

        public void SpendAction(int actionCost)
        {
            if (actionCost == 0 && ReactionsRemaining > 0) ReactionsRemaining--;
            else if (actionCost == 1 && ActionsRemaining > 0) ActionsRemaining--;
            else if (actionCost == 2 && BonusActionsRemaining > 0) BonusActionsRemaining--;
        }

        public void ResetForNewTurn()
        {
            ActionsRemaining = 1;
            BonusActionsRemaining = 0;
            ReactionsRemaining = 1;
            MovementRemaining = 30;
        }
    }

    /// <summary>
    /// Turn queue - ordered list of entities taking turns.
    /// </summary>
    public struct TurnQueueComponent : IComponentData
    {
        public const int MAX_COMBATANTS = 20;
        
        // Fixed array for determinism
        public int Combatant0, Combatant1, Combatant2, Combatant3, Combatant4;
        public int Combatant5, Combatant6, Combatant7, Combatant8, Combatant9;
        public int Combatant10, Combatant11, Combatant12, Combatant13, Combatant14;
        public int Combatant15, Combatant16, Combatant17, Combatant18, Combatant19;
        
        public int TotalCombatants;
        public int CurrentTurnIndex;

        public int GetCurrentActor()
        {
            if (CurrentTurnIndex >= TotalCombatants)
                return -1;

            return CurrentTurnIndex switch
            {
                0 => Combatant0, 1 => Combatant1, 2 => Combatant2, 3 => Combatant3, 4 => Combatant4,
                5 => Combatant5, 6 => Combatant6, 7 => Combatant7, 8 => Combatant8, 9 => Combatant9,
                10 => Combatant10, 11 => Combatant11, 12 => Combatant12, 13 => Combatant13, 14 => Combatant14,
                15 => Combatant15, 16 => Combatant16, 17 => Combatant17, 18 => Combatant18, 19 => Combatant19,
                _ => -1
            };
        }

        public void AddCombatant(int entityId)
        {
            if (TotalCombatants >= MAX_COMBATANTS)
                return;

            switch (TotalCombatants)
            {
                case 0: Combatant0 = entityId; break;
                case 1: Combatant1 = entityId; break;
                case 2: Combatant2 = entityId; break;
                case 3: Combatant3 = entityId; break;
                case 4: Combatant4 = entityId; break;
                case 5: Combatant5 = entityId; break;
                case 6: Combatant6 = entityId; break;
                case 7: Combatant7 = entityId; break;
                case 8: Combatant8 = entityId; break;
                case 9: Combatant9 = entityId; break;
                case 10: Combatant10 = entityId; break;
                case 11: Combatant11 = entityId; break;
                case 12: Combatant12 = entityId; break;
                case 13: Combatant13 = entityId; break;
                case 14: Combatant14 = entityId; break;
                case 15: Combatant15 = entityId; break;
                case 16: Combatant16 = entityId; break;
                case 17: Combatant17 = entityId; break;
                case 18: Combatant18 = entityId; break;
                case 19: Combatant19 = entityId; break;
            }
            TotalCombatants++;
        }

        public void AdvanceTurn()
        {
            CurrentTurnIndex++;
        }

        public bool IsRoundComplete()
        {
            return CurrentTurnIndex >= TotalCombatants;
        }

        public void ResetForNewRound()
        {
            CurrentTurnIndex = 0;
        }
    }

    /// <summary>
    /// Conditions/status effects on a combatant.
    /// </summary>
    public struct ConditionComponent : IComponentData
    {
        public const int MAX_CONDITIONS = 10;
        
        // Condition flags
        public bool IsProne;
        public bool IsStunned;
        public bool IsCharmed;
        public bool IsFrightened;
        public bool IsRestrained;
        public bool IsInvisible;
        public bool HasShield;
        public bool HasBless;
        public bool HasCurse;
        public uint ProneDuration;

        public int ActiveConditionCount;

        public bool HasCondition(string conditionName)
        {
            return conditionName switch
            {
                "Prone" => IsProne,
                "Stunned" => IsStunned,
                "Charmed" => IsCharmed,
                "Frightened" => IsFrightened,
                "Restrained" => IsRestrained,
                "Invisible" => IsInvisible,
                _ => false
            };
        }

        public void ApplyCondition(string conditionName)
        {
            switch (conditionName)
            {
                case "Prone": IsProne = true; break;
                case "Stunned": IsStunned = true; break;
                case "Charmed": IsCharmed = true; break;
                case "Frightened": IsFrightened = true; break;
                case "Restrained": IsRestrained = true; break;
                case "Invisible": IsInvisible = true; break;
            }
            ActiveConditionCount++;
        }

        public void RemoveCondition(string conditionName)
        {
            switch (conditionName)
            {
                case "Prone": IsProne = false; break;
                case "Stunned": IsStunned = false; break;
                case "Charmed": IsCharmed = false; break;
                case "Frightened": IsFrightened = false; break;
                case "Restrained": IsRestrained = false; break;
                case "Invisible": IsInvisible = false; break;
            }
            if (ActiveConditionCount > 0)
                ActiveConditionCount--;
        }
    }

    /// <summary>
    /// Combat round state and phase management.
    /// </summary>
    public struct CombatRoundComponent : IComponentData
    {
        public int ActiveCombatantId;
        public int RoundNumber;
        public int TotalParticipants;
        public int CurrentTurnIndex;
        
        // Phase: 0=Initialize, 1=Action, 2=Resolution, 3=TurnEnd, 4=RoundEnd, 5=CombatEnd
        public int CombatPhase;
        public uint PhaseStartFrame;
        public int ActionsThisRound;
        public int DamageThisRound;
        public bool IsFinalRound;
    }
}
