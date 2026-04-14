using DunGen.ECS.Combat;
using DunGen.ECS.Exploration;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Simulation.RNG;
using Unity.Entities;
using UnityEngine;

namespace DunGen.ECS.Systems
{
    /// <summary>
    /// Handles player and NPC movement, collision detection.
    /// </summary>
    public partial class MovementSystem : SystemBase
    {
        private DeterministicRNG _rng;

        public MovementSystem()
        {
            _rng = new DeterministicRNG();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .ForEach((ref PositionComponent pos, in MovementComponent movement) =>
                {
                    // Reset movement counter each turn
                    if (movement.TilesMovedThisTurn >= movement.MovementSpeed)
                        return;  // Can't move anymore this turn
                    
                    // Movement logic handled by game loop (input-based for player, AI-based for enemies)
                }).Run();
        }
    }

    /// <summary>
    /// Simple enemy AI - wander or pursue player.
    /// </summary>
    public partial class EnemyAISystem : SystemBase
    {
        private DeterministicRNG _rng;
        private EventBus _eventBus;

        public EnemyAISystem()
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

            // For now, simple behavior: enemies wander randomly
            // In full game: pathfind to player
            Entities
                .WithoutBurst()
                .ForEach((ref PositionComponent pos, ref MovementComponent movement, in CombatComponent combat) =>
                {
                    if (combat.IsDead || movement.TilesMovedThisTurn >= movement.MovementSpeed)
                        return;

                    // Random walk
                    int moveX = _rng.RollDice(3) - 2;  // -1, 0, or 1
                    int moveY = _rng.RollDice(3) - 2;
                    
                    pos.X += moveX;
                    pos.Y += moveY;
                    movement.TilesMovedThisTurn++;

                }).Run();
        }
    }

    /// <summary>
    /// Detect collisions between entities (combat initiation).
    /// </summary>
    public partial class CollisionDetectionSystem : SystemBase
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

            // Simplified: just check if two entities occupy same tile
            // In full game: spatial partitioning for efficiency
            Entities
                .WithoutBurst()
                .ForEach((in PositionComponent pos1, in CombatComponent combat1) =>
                {
                    // Check for collisions with other entities
                    // Trigger combat if enemy
                }).Run();
        }
    }

    /// <summary>
    /// Handles loot drops and item pickup.
    /// </summary>
    public partial class LootSystem : SystemBase
    {
        private EventBus _eventBus;
        private DeterministicRNG _rng;

        public LootSystem()
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

            Entities
                .WithoutBurst()
                .ForEach((in CombatComponent combat, in LootTableComponent loot) =>
                {
                    // When enemy dies, drop loot
                    if (combat.IsDead && loot.DropOnDeath)
                    {
                        int goldDrop = _rng.RollDice(loot.GoldDropMax - loot.GoldDropMin + 1) + loot.GoldDropMin;
                        
                        // Publish loot event
                        _eventBus?.Publish(new GameEvent
                        {
                            EventId = _eventBus.GetNextEventId(),
                            FrameNumber = (uint)Time.frameCount,
                            Timestamp = (uint)Time.frameCount / 60f,
                            EventType = 99  // Custom: Loot event
                        });
                    }
                }).Run();
        }
    }

    /// <summary>
    /// Experience gain and leveling.
    /// </summary>
    public partial class ExperienceSystem : SystemBase
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
                .ForEach((ref ExperienceComponent exp, in CombatComponent combat) =>
                {
                    // When player defeats enemy, gain XP
                    // Simplified: 50 XP per enemy
                    if (combat.IsInCombat && !combat.IsDead)
                    {
                        exp.CurrentXP += 50;
                        
                        while (exp.CanLevelUp())
                        {
                            exp.LevelUp();
                            
                            // Publish level up event
                            _eventBus?.Publish(new GameEvent
                            {
                                EventId = _eventBus.GetNextEventId(),
                                FrameNumber = (uint)Time.frameCount,
                                Timestamp = (uint)Time.frameCount / 60f,
                                EventType = 100  // Custom: Level up event
                            });
                        }
                    }
                }).Run();
        }
    }

    /// <summary>
    /// Exploration and encounter generation.
    /// </summary>
    public partial class ExplorationSystem : SystemBase
    {
        private EventBus _eventBus;
        private DeterministicRNG _rng;

        public ExplorationSystem()
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

            // Track exploration progress
            Entities
                .WithoutBurst()
                .ForEach((in PositionComponent pos, in VisionComponent vision) =>
                {
                    // Player explores tiles within vision range
                    // Mark as explored, detect encounters
                    if (vision.IsPlayerControlled)
                    {
                        // Random encounter chance
                        int encounter = _rng.RollDice(100);
                        if (encounter < 15)  // 15% encounter rate
                        {
                            // Trigger combat encounter
                        }
                    }
                }).Run();
        }
    }
}
