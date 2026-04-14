namespace DunGen.Events.Combat
{
    /// <summary>
    /// Data-oriented combat event structures (pure data, no methods).
    /// Part of ECS with deterministic replay capability.
    /// </summary>

    /// <summary>Event: Combat session begins with initiative rolls.</summary>
    public struct CombatStartedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int[] ParticipantEntityIds;
        public int[] InitiativeOrder;
        public int CombatSessionId;
    }

    /// <summary>Event: Single combatant's initiative is rolled.</summary>
    public struct InitiativeRolledEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int ActorEntityId;
        public int D20Roll;
        public int DexModifier;
        public int FinalInitiativeScore;
        public int TurnPosition;
    }

    /// <summary>Event: Combatant attempts attack roll (melee or ranged).</summary>
    public struct AttackResolvedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int AttackerEntityId;
        public int DefenderEntityId;
        public int D20Roll;
        public int AttackModifier;
        public int TargetAC;
        public int FinalAttackRoll;
        public bool IsHit;
        public bool IsNaturalTwenty;
        public bool IsNaturalOne;
        public string WeaponName;
        public int DamageIfHit;
    }

    /// <summary>Event: Damage calculated and applied to combatant.</summary>
    public struct DamageInflictedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int VictimEntityId;
        public int DamageDealt;
        public string DamageType;
        public float DamageMultiplier;
        public int BaseDamage;
        public string DamageSource;
        public int VictimHealthRemaining;
    }

    /// <summary>Event: Combatant healed (spell, ability, potion, etc).</summary>
    public struct HealingReceivedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int RecipientEntityId;
        public int HealingAmount;
        public string HealingSource;
        public int RecipientHealthRemaining;
        public int OverhealingWasted;
    }

    /// <summary>Event: Combatant defeated (health reaches 0 or below).</summary>
    public struct DeathEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int DeceasedEntityId;
        public int KillerEntityId;
        public int[] SurvivingCombatants;
        public string CauseOfDeath;
    }

    /// <summary>Event: Combat ends (victory condition met or truce).</summary>
    public struct CombatEndedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int CombatSessionId;
        public int[] VictorIds;
        public int[] DefeatedIds;
        public string EndReason;
        public int TotalRoundsElapsed;
    }

    /// <summary>Event: Turn begins for specific combatant.</summary>
    public struct TurnStartedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int ActorEntityId;
        public int RoundNumber;
        public int TurnIndex;
    }

    /// <summary>Event: Round counter increments (all combatants have acted).</summary>
    public struct RoundEndedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int RoundNumber;
        public int ParticipantCount;
    }

    /// <summary>Event: Spell is cast (mana consumed, effect applied).</summary>
    public struct SpellCastEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int CasterEntityId;
        public int[] TargetEntityIds;
        public string SpellName;
        public int ManaCost;
        public int CasterManaRemaining;
    }

    /// <summary>Event: Entity uses an item (consumable, buff, etc).</summary>
    public struct ItemUsedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int UserEntityId;
        public string ItemName;
        public int QuantityUsed;
        public int QuantityRemaining;
    }

    /// <summary>Event: Combatant applies status effect (buff, debuff, condition).</summary>
    public struct StatusEffectAppliedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int TargetEntityId;
        public string EffectName;
        public float Duration;
        public float Potency;
        public int StackCount;
    }
}
