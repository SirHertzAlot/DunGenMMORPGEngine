using System;
using Unity.Entities;

namespace DunGen.Events
{
    /// <summary>
    /// Base class for all game events.
    /// Events represent all state-changing actions in the simulation.
    /// All events are deterministically logged and can be replayed.
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>Unique event ID for sequencing.</summary>
        public ulong EventId { get; set; }
        
        /// <summary>Simulation frame when event occurred.</summary>
        public uint FrameNumber { get; set; }
        
        /// <summary>Timestamp (in seconds) when event occurred.</summary>
        public float Timestamp { get; set; }
        
        /// <summary>Entity that triggered this event, if any.</summary>
        public Entity SourceEntity { get; set; }

        /// <summary>Get human-readable event type name.</summary>
        public abstract string GetEventTypeName();
        
        /// <summary>Serialize event to JSON-compatible format.</summary>
        public abstract string ToJsonString();
    }

    /// <summary>Event: Simulation initialized with seed.</summary>
    public class SimulationInitializedEvent : GameEvent
    {
        public ulong Seed { get; set; }
        public int MaxEntities { get; set; }

        public override string GetEventTypeName() => "SimulationInitialized";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"SimulationInitialized\",\"seed\":{Seed},\"maxEntities\":{MaxEntities},\"frameNumber\":{FrameNumber}}}";
        }
    }

    /// <summary>Event: Entity created.</summary>
    public class EntityCreatedEvent : GameEvent
    {
        public string EntityType { get; set; }
        public string Name { get; set; }

        public override string GetEventTypeName() => "EntityCreated";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"EntityCreated\",\"entityId\":\"{SourceEntity.Index}\",\"entityType\":\"{EntityType}\",\"name\":\"{Name}\",\"frameNumber\":{FrameNumber}}}";
        }
    }

    /// <summary>Event: Entity moved.</summary>
    public class EntityMovedEvent : GameEvent
    {
        public float FromX { get; set; }
        public float FromY { get; set; }
        public float ToX { get; set; }
        public float ToY { get; set; }

        public override string GetEventTypeName() => "EntityMoved";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"EntityMoved\",\"entityId\":\"{SourceEntity.Index}\",\"from\":[{FromX},{FromY}],\"to\":[{ToX},{ToY}],\"frameNumber\":{FrameNumber}}}";
        }
    }

    /// <summary>Event: Attack resolved.</summary>
    public class AttackEvent : GameEvent
    {
        public Entity TargetEntity { get; set; }
        public int AttackRoll { get; set; }
        public int DamageRoll { get; set; }
        public bool Hit { get; set; }
        public int TargetAC { get; set; }

        public override string GetEventTypeName() => "Attack";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"Attack\",\"attackerId\":\"{SourceEntity.Index}\",\"targetId\":\"{TargetEntity.Index}\",\"roll\":{AttackRoll},\"ac\":{TargetAC},\"damage\":{DamageRoll},\"hit\":{(Hit ? "true" : "false")},\"frameNumber\":{FrameNumber}}}";
        }
    }

    /// <summary>Event: Damage taken.</summary>
    public class DamageTakenEvent : GameEvent
    {
        public int DamageAmount { get; set; }
        public int RemainingHealth { get; set; }
        public int MaxHealth { get; set; }

        public override string GetEventTypeName() => "DamageTaken";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"DamageTaken\",\"entityId\":\"{SourceEntity.Index}\",\"damage\":{DamageAmount},\"remainingHealth\":{RemainingHealth},\"maxHealth\":{MaxHealth},\"frameNumber\":{FrameNumber}}}";
        }
    }

    /// <summary>Event: Entity died.</summary>
    public class EntityDiedEvent : GameEvent
    {
        public override string GetEventTypeName() => "EntityDied";
        public override string ToJsonString()
        {
            return $"{{\"type\":\"EntityDied\",\"entityId\":\"{SourceEntity.Index}\",\"frameNumber\":{FrameNumber}}}";
        }
    }
}
