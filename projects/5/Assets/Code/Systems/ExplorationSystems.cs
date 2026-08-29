using DunGen.ECS.Combat;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Simulation.RNG;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace DunGen.ECS.Systems
{
    /// <summary>
    /// Handles player and NPC movement, collision detection.
    /// </summary>
    public partial class MovementSystem : SystemBase
    {
        private DeterministicRNG _rng;
        private SpatialHashGrid _spatialGrid;
        private EntityIndexCache _entityCache;

        public MovementSystem()
        {
            _rng = new DeterministicRNG();
            _spatialGrid = SpatialHashGrid.Instance;
            _entityCache = EntityIndexCache.Instance;
        }

        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .ForEach((Entity entity, ref PositionComponent pos, ref MovementComponent movement) =>
                {
                    if (movement.MovementSpeed < 0)
                        movement.MovementSpeed = 0;

                    if (movement.TilesMovedThisTurn < 0)
                        movement.TilesMovedThisTurn = 0;
                    else if (movement.TilesMovedThisTurn > movement.MovementSpeed)
                        movement.TilesMovedThisTurn = movement.MovementSpeed;

                    pos.X = Mathf.Clamp(pos.X, 1, 78);
                    pos.Y = Mathf.Clamp(pos.Y, 1, 22);
                    _entityCache.Register(entity);
                    _spatialGrid.UpdatePosition(entity.Index, pos.X, pos.Y, pos.DungeonLevel);
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
        private SpatialHashGrid _spatialGrid;
        private EntityIndexCache _entityCache;

        public EnemyAISystem()
        {
            _rng = new DeterministicRNG();
            _spatialGrid = SpatialHashGrid.Instance;
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

            bool hasPlayer = _entityCache.TryGetPlayerEntity(out var playerEntity) &&
                EntityManager.HasComponent<PositionComponent>(playerEntity);
            var playerPosition = hasPlayer
                ? EntityManager.GetComponentData<PositionComponent>(playerEntity)
                : default;

            Entities
                .WithoutBurst()
                .ForEach((Entity entity, ref PositionComponent pos, ref MovementComponent movement, in CombatComponent combat) =>
                {
                    if (combat.IsDead || movement.TilesMovedThisTurn >= movement.MovementSpeed)
                        return;

                    int moveX;
                    int moveY;
                    if (hasPlayer && pos.DungeonLevel == playerPosition.DungeonLevel)
                    {
                        int distance = Mathf.Abs(playerPosition.X - pos.X) + Mathf.Abs(playerPosition.Y - pos.Y);
                        if (distance <= 8)
                        {
                            moveX = System.Math.Sign(playerPosition.X - pos.X);
                            moveY = System.Math.Sign(playerPosition.Y - pos.Y);
                        }
                        else
                        {
                            moveX = _rng.RollDice(3) - 2;
                            moveY = _rng.RollDice(3) - 2;
                        }
                    }
                    else
                    {
                        moveX = _rng.RollDice(3) - 2;
                        moveY = _rng.RollDice(3) - 2;
                    }
                    
                    pos.X = Mathf.Clamp(pos.X + moveX, 1, 78);
                    pos.Y = Mathf.Clamp(pos.Y + moveY, 1, 22);
                    movement.TilesMovedThisTurn++;

                    _spatialGrid.UpdatePosition(entity.Index, pos.X, pos.Y, pos.DungeonLevel);

                }).Run();
        }
    }

    /// <summary>
    /// Detect collisions between entities (combat initiation).
    /// Uses SpatialHashGrid for O(n) detection instead of O(n²).
    /// </summary>
    public partial class CollisionDetectionSystem : SystemBase
    {
        private EventBus _eventBus;
        private int _nextCombatSessionId = 1000;
        private SpatialHashGrid _spatialGrid;
        private EntityIndexCache _entityCache;

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
            _spatialGrid = SpatialHashGrid.Instance;
            _entityCache = EntityIndexCache.Instance;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PositionComponent>(),
                ComponentType.ReadOnly<CombatComponent>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var positions = query.ToComponentDataArray<PositionComponent>(Allocator.Temp);
            using var combats = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);

            // First pass: update spatial grid and collect valid collision candidates
            for (int i = 0; i < entities.Length; i++)
            {
                _entityCache.Register(entities[i]);
                _spatialGrid.UpdatePosition(entities[i].Index, positions[i].X, positions[i].Y, positions[i].DungeonLevel);
            }

            // Second pass: O(n) collision detection using spatial hash
            // Each entity checks only its own cell for collisions
            for (int i = 0; i < entities.Length; i++)
            {
                if (combats[i].IsDead || combats[i].IsInCombat)
                    continue;

                // O(1) lookup: get entities at same position
                if (!_spatialGrid.GetEntitiesAt(positions[i].X, positions[i].Y, positions[i].DungeonLevel, out var sameCellEntities))
                    continue;

                foreach (var otherIndex in sameCellEntities)
                {
                    // Skip self and already-processed pairs (only check if other index > current)
                    if (otherIndex <= entities[i].Index)
                        continue;

                    if (!_entityCache.TryGetEntity(otherIndex, out var otherEntity))
                        continue;

                    if (!EntityManager.HasComponent<CombatComponent>(otherEntity))
                        continue;

                    var otherCombat = EntityManager.GetComponentData<CombatComponent>(otherEntity);
                    if (otherCombat.IsDead || otherCombat.IsInCombat)
                        continue;

                    CombatEncounterUtility.StartCombat(EntityManager, entities[i], otherEntity, _nextCombatSessionId++, (uint)UnityEngine.Time.frameCount);
                }
            }
        }
    }

    /// <summary>
    /// Handles loot drops and item pickup.
    /// </summary>
    public partial class LootSystem : SystemBase
    {
        private EventBus _eventBus;
        private DeterministicRNG _rng;
        private EntityIndexCache _entityCache;

        public LootSystem()
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

            var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CombatComponent>(),
                typeof(LootTableComponent));

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var combats = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);
            using var loots = query.ToComponentDataArray<LootTableComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var loot = loots[i];
                if (combats[i].IsDead && loot.DropOnDeath)
                {
                    int minGold = Mathf.Min(loot.GoldDropMin, loot.GoldDropMax);
                    int maxGold = Mathf.Max(loot.GoldDropMin, loot.GoldDropMax);
                    int goldDrop = minGold + _rng.RollDice(maxGold - minGold + 1) - 1;
                    int recipientEntityId = entities[i].Index;

                    if (_entityCache.TryGetPlayerEntity(out var playerEntity))
                    {
                        recipientEntityId = playerEntity.Index;

                        if (EntityManager.HasComponent<CurrencyComponent>(playerEntity))
                        {
                            var currency = EntityManager.GetComponentData<CurrencyComponent>(playerEntity);
                            currency.AddGold(goldDrop);
                            EntityManager.SetComponentData(playerEntity, currency);
                        }
                    }

                    _eventBus?.Publish(new LootGrantedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        RecipientEntityId = recipientEntityId,
                        LootTableId = loot.LootTableId,
                        GoldAmount = goldDrop
                    });

                    loot.DropOnDeath = false;
                    EntityManager.SetComponentData(entities[i], loot);
                }
            }
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

            var query = EntityManager.CreateEntityQuery(
                typeof(ExperienceComponent),
                ComponentType.ReadOnly<CombatComponent>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var experiences = query.ToComponentDataArray<ExperienceComponent>(Allocator.Temp);
            using var combats = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var exp = experiences[i];
                var combat = combats[i];
                if (combat.IsInCombat &&
                    !combat.IsDead &&
                    combat.CombatSessionId > 0 &&
                    exp.LastRewardedCombatSessionId != combat.CombatSessionId)
                {
                    int previousLevel = exp.Level;
                    exp.CurrentXP += 50;
                    exp.LastRewardedCombatSessionId = combat.CombatSessionId;

                    while (exp.CanLevelUp())
                    {
                        exp.LevelUp();

                        _eventBus?.Publish(new LevelUpEventData
                        {
                            EventId = _eventBus.GetNextEventId(),
                            FrameNumber = (uint)UnityEngine.Time.frameCount,
                            Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                            EntityId = entities[i].Index,
                            PreviousLevel = previousLevel,
                            NewLevel = exp.Level,
                            RemainingXP = exp.CurrentXP,
                            XPToNextLevel = exp.XPToNextLevel
                        });

                        previousLevel = exp.Level;
                    }

                    EntityManager.SetComponentData(entities[i], exp);
                }
            }
        }
    }

    /// <summary>
    /// Exploration and encounter generation.
    /// Uses SpatialHashGrid for O(range) nearby entity lookups instead of O(n).
    /// </summary>
    public partial class ExplorationSystem : SystemBase
    {
        private EventBus _eventBus;
        private DeterministicRNG _rng;
        private int _nextCombatSessionId = 5000;
        private SpatialHashGrid _spatialGrid;
        private EntityIndexCache _entityCache;

        public ExplorationSystem()
        {
            _rng = new DeterministicRNG();
            _spatialGrid = SpatialHashGrid.Instance;
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

            var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PositionComponent>(),
                ComponentType.ReadOnly<VisionComponent>(),
                ComponentType.ReadOnly<CombatComponent>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var positions = query.ToComponentDataArray<PositionComponent>(Allocator.Temp);
            using var visions = query.ToComponentDataArray<VisionComponent>(Allocator.Temp);
            using var combats = query.ToComponentDataArray<CombatComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!visions[i].IsPlayerControlled || combats[i].IsDead || combats[i].IsInCombat)
                    continue;

                int encounter = _rng.RollDice(100);
                if (encounter >= 15)
                    continue;

                if (TryFindEncounterTarget(entities[i], positions[i], out var targetEntity))
                    CombatEncounterUtility.StartCombat(EntityManager, entities[i], targetEntity, _nextCombatSessionId++, (uint)UnityEngine.Time.frameCount);
            }
        }

        /// <summary>
        /// Find closest valid encounter target using spatial grid.
        /// O(range) instead of O(n) for nearby lookups.
        /// </summary>
        private bool TryFindEncounterTarget(Entity sourceEntity, PositionComponent sourcePosition, out Entity targetEntity)
        {
            const int MaxEncounterRange = 20; // Only check entities within range

            // O(range) lookup via spatial grid
            int closestIndex = _spatialGrid.FindClosestEntity(
                sourcePosition.X,
                sourcePosition.Y,
                sourcePosition.DungeonLevel,
                MaxEncounterRange,
                sourceEntity.Index);

            if (closestIndex < 0 || !_entityCache.TryGetEntity(closestIndex, out targetEntity))
            {
                targetEntity = Entity.Null;
                return false;
            }

            // Validate the target is still valid for combat
            if (!EntityManager.HasComponent<CombatComponent>(targetEntity))
            {
                targetEntity = Entity.Null;
                return false;
            }

            var combat = EntityManager.GetComponentData<CombatComponent>(targetEntity);
            if (combat.IsDead || combat.IsInCombat)
            {
                targetEntity = Entity.Null;
                return false;
            }

            return true;
        }
    }

    internal static class CombatEncounterUtility
    {
        public static void StartCombat(EntityManager entityManager, Entity first, Entity second, int sessionId, uint frameNumber)
        {
            var rng = new DeterministicRNG(frameNumber + (uint)sessionId);
            PrepareCombatant(entityManager, first, sessionId, rng.RollD20());
            PrepareCombatant(entityManager, second, sessionId, rng.RollD20());
        }

        private static void PrepareCombatant(EntityManager entityManager, Entity entity, int sessionId, int initiativeRoll)
        {
            if (!entityManager.HasComponent<CombatComponent>(entity))
                return;

            var combat = entityManager.GetComponentData<CombatComponent>(entity);
            combat.IsInCombat = true;
            combat.CombatSessionId = sessionId;
            combat.CombatSeed = (uint)sessionId;
            combat.CurrentRound = 1;
            combat.HasActedThisRound = false;
            entityManager.SetComponentData(entity, combat);

            CombatStatsComponent stats = entityManager.HasComponent<CombatStatsComponent>(entity)
                ? entityManager.GetComponentData<CombatStatsComponent>(entity)
                : default;

            EnsureComponent(entityManager, entity, new InitiativeComponent
            {
                DexModifier = stats.DexterityModifier,
                D20Roll = initiativeRoll,
                InitiativeScore = initiativeRoll + stats.DexterityModifier,
                TurnOrder = 0
            });

            EnsureComponent(entityManager, entity, new CombatRoundComponent
            {
                ActiveCombatantId = entity.Index,
                RoundNumber = 1,
                TotalParticipants = 2,
                CurrentTurnIndex = 0,
                CombatPhase = 0,
                ActionsThisRound = 0,
                DamageThisRound = 0
            });

            EnsureComponent(entityManager, entity, new ActionQueueComponent());

            var costs = new ActionCostComponent();
            costs.ResetForNewTurn();
            EnsureComponent(entityManager, entity, costs);

            EnsureComponent(entityManager, entity, new ConditionComponent());
        }

        private static void EnsureComponent<T>(EntityManager entityManager, Entity entity, T value) where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
        }
    }
}
