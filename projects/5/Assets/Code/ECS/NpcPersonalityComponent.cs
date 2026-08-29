using Unity.Collections;
using Unity.Entities;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Per-NPC personality traits seeded deterministically at spawn time.
    /// All values 0-100.  Higher = stronger expression of that trait.
    ///
    /// Trait summary:
    ///   Aggression   – engage range, willingness to start fights
    ///   Cowardice    – flee threshold (HP %)
    ///   Greed        – target wealthiest player / pick up loot before engaging
    ///   Loyalty      – aid nearby allies before pursuing the player
    ///   Curiosity    – investigate events rather than charging
    ///   Vengefulness – prioritise whoever last damaged this NPC
    /// </summary>
    public struct NpcPersonalityComponent : IComponentData
    {
        public byte Aggression;
        public byte Cowardice;
        public byte Greed;
        public byte Loyalty;
        public byte Curiosity;
        public byte Vengefulness;

        /// <summary>Archetype tag baked in at generation time (e.g. "goblin").</summary>
        public FixedString32Bytes ArchetypeName;

        // ---- computed helpers (no allocations) --------------------------------

        /// <summary>Tile radius within which this NPC will start chasing (4-9).</summary>
        public int AggressionRange => 4 + (Aggression / 20);

        /// <summary>Fraction of max-HP below which a cowardly NPC will flee (0-0.5).</summary>
        public float FleeThreshold => Cowardice > 40 ? (Cowardice / 200f) : 0f;

        public bool WillFlee(float currentHp, float maxHp) =>
            FleeThreshold > 0f && (currentHp / maxHp) < FleeThreshold;

        public bool PrioritisesAllies => Loyalty > 60;
        public bool InvestigatesFirst => Curiosity > 55;
        public bool IsVengeful => Vengefulness > 50;
        public bool IsGreedy => Greed > 60;
    }

    /// <summary>
    /// Tracks mutable per-NPC world state that accumulates across turns.
    /// </summary>
    public struct NpcWorldStateComponent : IComponentData
    {
        /// <summary>Entity index of whoever last damaged this NPC (0 = none).</summary>
        public int LastDamagedByEntityIndex;

        /// <summary>Consecutive turns this NPC has been in flee mode.</summary>
        public int FleeingTurns;

        /// <summary>
        /// Running "tension" value (0-100).  Rises with witnessed violence,
        /// falls when calm.  Used by WorldMoodEventData aggregation.
        /// </summary>
        public byte LocalTension;

        /// <summary>True after this NPC has reacted to an event this turn.</summary>
        public bool HasReactedThisTurn;
    }
}
