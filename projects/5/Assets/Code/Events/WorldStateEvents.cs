namespace DunGen.Events.World
{
    /// <summary>
    /// Reaction decisions an NPC can make in response to a world event.
    /// Multiple NPCs may react differently to the same stimulus.
    /// </summary>
    public enum NpcReactionType
    {
        None       = 0,
        Engage     = 1,  // move toward threat and fight
        Flee       = 2,  // move away from threat
        Aid        = 3,  // move toward injured ally
        Taunt      = 4,  // signal aggression without immediate attack (raises tension)
        Investigate = 5, // move toward disturbance without immediately engaging
        Guard      = 6,  // hold position, defend nearby allies
        Surrender  = 7,  // cease combat, non-hostile
    }

    /// <summary>
    /// Published when an NPC decides how to respond to a world event.
    /// Other NPCs nearby can subscribe and chain-react to this.
    /// </summary>
    public struct NpcReactionEventData
    {
        public ulong EventId;
        public float Timestamp;

        /// <summary>ECS entity index of the reacting NPC.</summary>
        public int ReactingEntityIndex;
        public string ArchetypeName;

        /// <summary>ECS entity index of the stimulus source (0 = environmental).</summary>
        public int StimulusEntityIndex;

        public NpcReactionType Reaction;

        /// <summary>Personality snapshot for logging / UI display.</summary>
        public byte Aggression;
        public byte Cowardice;
        public byte Loyalty;
        public byte Vengefulness;

        /// <summary>Tile position of reacting NPC at reaction time.</summary>
        public int TileX;
        public int TileY;
        public int DungeonLevel;
    }

    /// <summary>
    /// Published when a critical mass of NPC reactions tips faction mood.
    /// Front-end can react with music/lighting/NPC dialogue cues.
    /// </summary>
    public struct FactionStateChangedEventData
    {
        public ulong EventId;
        public float Timestamp;
        public string FactionArchetype; // e.g. "goblin"
        public string OldMood;          // e.g. "neutral"
        public string NewMood;          // e.g. "hostile" | "fleeing" | "rallying"
        public int MemberCount;
    }

    /// <summary>
    /// Aggregate world tension snapshot, published each turn.
    /// 0 = calm, 100 = total chaos.
    /// </summary>
    public struct WorldMoodEventData
    {
        public ulong EventId;
        public float Timestamp;
        public byte GlobalTension;   // 0-100
        public int ActiveCombatants; // NPCs currently in combat
        public int FleingNpcs;
        public int DeadThisTurn;
    }
}
