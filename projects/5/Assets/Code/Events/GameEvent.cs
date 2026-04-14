using Unity.Entities;

namespace DunGen.Events
{
    /// <summary>
    /// Data-oriented event structures (no inheritance, no virtual methods).
    /// Events are pure data containers for deterministic simulation replay.
    /// </summary>

    /// <summary>Event: Simulation initialized with seed.</summary>
    public struct SimulationInitializedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public ulong Seed;
        public int MaxEntities;
    }

    /// <summary>Event: Entity created.</summary>
    public struct EntityCreatedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public string EntityType;
        public string Name;
    }

    /// <summary>Event: Entity moved.</summary>
    public struct EntityMovedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public float FromX;
        public float FromY;
        public float ToX;
        public float ToY;
    }

    /// <summary>Event: Attack resolved.</summary>
    public struct AttackEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public int AttackRoll;
        public int DamageRoll;
        public bool Hit;
        public int TargetAC;
    }

    /// <summary>Event: Damage taken.</summary>
    public struct DamageTakenEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public int DamageAmount;
        public int RemainingHealth;
        public int MaxHealth;
    }

    /// <summary>Event: Entity died.</summary>
    public struct EntityDiedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
    }
}
