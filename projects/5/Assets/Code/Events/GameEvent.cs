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

    /// <summary>Event: Entity spawned into the playable world/session.</summary>
    public struct EntitySpawnedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public string EntityType;
        public string SpawnReason;
        public int X;
        public int Y;
        public int DungeonLevel;
    }

    /// <summary>Event: Entity removed from the playable world/session.</summary>
    public struct EntityDestroyedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public Entity SourceEntity;
        public string EntityType;
        public string Reason;
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

    /// <summary>Event: Game session started.</summary>
    public struct GameSessionStartedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public ulong Seed;
        public int LevelNumber;
    }

    /// <summary>Event: Game turn started.</summary>
    public struct GameTurnStartedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int TurnNumber;
        public int LevelNumber;
        public int LivingEnemyCount;
    }

    /// <summary>Event: Game turn completed.</summary>
    public struct GameTurnCompletedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int TurnNumber;
        public int LevelNumber;
        public int LivingEnemyCount;
        public bool IsGameOver;
    }

    /// <summary>Event: Dungeon level generation completed.</summary>
    public struct DungeonLevelGeneratedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int LevelNumber;
        public int Width;
        public int Height;
        public int TileCount;
        public int EnemyBudget;
        public int LootBudget;
        public int Seed;
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

    /// <summary>Event: Loot granted to an entity.</summary>
    public struct LootGrantedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int RecipientEntityId;
        public int LootTableId;
        public int GoldAmount;
    }

    /// <summary>Event: Experience-based level up occurred.</summary>
    public struct LevelUpEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public int EntityId;
        public int PreviousLevel;
        public int NewLevel;
        public int RemainingXP;
        public int XPToNextLevel;
    }
}
