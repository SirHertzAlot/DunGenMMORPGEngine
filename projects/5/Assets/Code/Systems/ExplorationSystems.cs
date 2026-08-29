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

        public MovementSystem()
        {
            _rng = new DeterministicRNG();
            _spatialGrid = SpatialHashGrid.Instance;
        }

        protected override void OnUpdate()
        {
            foreach (var (posRW, movRW, entity) in SystemAPI.Query<RefRW<PositionComponent>, RefRW<MovementComponent>>().WithEntityAccess())
            {
                ref var pos = ref posRW.ValueRW;
                ref var movement = ref movRW.ValueRW;

                if (movement.MovementSpeed < 0)
                    movement.MovementSpeed = 0;

                if (movement.TilesMovedThisTurn < 0)
                    movement.TilesMovedThisTurn = 0;
                else if (movement.TilesMovedThisTurn > movement.MovementSpeed)
                    movement.TilesMovedThisTurn = movement.MovementSpeed;

                pos.X = Mathf.Clamp(pos.X, 1, 78);
                pos.Y = Mathf.Clamp(pos.Y, 1, 22);
                _spatialGrid.UpdatePosition(entity.Index, pos.X, pos.Y, pos.DungeonLevel);
            }
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
            _eventBus = EventBus.Instance;
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

            foreach (var (posRW, movRW, combatRO, persRO, stateRW, entity) in SystemAPI
                .Query<RefRW<PositionComponent>, RefRW<MovementComponent>, RefRO<CombatComponent>,
                       RefRO<NpcPersonalityComponent>, RefRW<NpcWorldStateComponent>>()
                .WithEntityAccess())
            {
                ref var pos      = ref posRW.ValueRW;
                ref var movement = ref movRW.ValueRW;
                ref var state    = ref stateRW.ValueRW;
                var combat       = combatRO.ValueRO;
                var personality  = persRO.ValueRO;

                if (combat.IsDead || movement.TilesMovedThisTurn >= movement.MovementSpeed)
                    continue;

                int moveX = 0;
                int moveY = 0;

                if (hasPlayer && pos.DungeonLevel == playerPosition.DungeonLevel)
                {
                    int distance = Mathf.Abs(playerPosition.X - pos.X) + Mathf.Abs(playerPosition.Y - pos.Y);
                    int aggressionRange = personality.AggressionRange; // 4-9 based on Aggression trait

                    // --- Flee: cowardly NPC at low HP runs away ---
                    if (personality.WillFlee(combat.CurrentHealth, combat.MaxHealth))
                    {
                        // Move away from player
                        moveX = -System.Math.Sign(playerPosition.X - pos.X);
                        moveY = -System.Math.Sign(playerPosition.Y - pos.Y);
                        state.FleeingTurns++;
                    }
                    // --- Aid: loyal NPC looks for a hurt ally instead ---
                    else if (personality.PrioritisesAllies && TryFindWoundedAlly(entity, pos, out var allyPos))
                    {
                        moveX = System.Math.Sign(allyPos.X - pos.X);
                        moveY = System.Math.Sign(allyPos.Y - pos.Y);
                        state.FleeingTurns = 0;
                    }
                    // --- Vengeance: vengeful NPC targets whoever last hit them ---
                    else if (personality.IsVengeful && state.LastDamagedByEntityIndex != 0 &&
                             TryGetEntityPosition(state.LastDamagedByEntityIndex, pos.DungeonLevel, out var attackerPos))
                    {
                        moveX = System.Math.Sign(attackerPos.X - pos.X);
                        moveY = System.Math.Sign(attackerPos.Y - pos.Y);
                        state.FleeingTurns = 0;
                    }
                    // --- Curiosity: curious NPCs wander toward interesting events, not directly at player ---
                    else if (personality.InvestigatesFirst && distance > aggressionRange)
                    {
                        // Random drift with a bias toward player direction
                        int biasX = System.Math.Sign(playerPosition.X - pos.X);
                        int biasY = System.Math.Sign(playerPosition.Y - pos.Y);
                        moveX = _rng.RollDice(3) > 2 ? biasX : _rng.RollDice(3) - 2;
                        moveY = _rng.RollDice(3) > 2 ? biasY : _rng.RollDice(3) - 2;
                        state.FleeingTurns = 0;
                    }
                    // --- Greedy: greedy NPCs circle to the player's side (diagonal) ---
                    else if (personality.IsGreedy && distance <= aggressionRange + 2)
                    {
                        moveX = System.Math.Sign(playerPosition.X - pos.X);
                        moveY = _rng.RollDice(3) - 2; // erratic Y to flank
                        state.FleeingTurns = 0;
                    }
                    // --- Default chase within aggression range ---
                    else if (distance <= aggressionRange)
                    {
                        moveX = System.Math.Sign(playerPosition.X - pos.X);
                        moveY = System.Math.Sign(playerPosition.Y - pos.Y);
                        state.FleeingTurns = 0;
                    }
                    else
                    {
                        moveX = _rng.RollDice(3) - 2;
                        moveY = _rng.RollDice(3) - 2;
                        state.FleeingTurns = 0;
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
            }
        }

        private bool TryFindWoundedAlly(Entity self, PositionComponent selfPos, out PositionComponent allyPos)
        {
            allyPos = default;
            foreach (var (posRO, combatRO, entity) in SystemAPI
                .Query<RefRO<PositionComponent>, RefRO<CombatComponent>>()
                .WithAll<NpcPersonalityComponent>()
                .WithEntityAccess())
            {
                if (entity == self) continue;
                var c = combatRO.ValueRO;
                if (c.IsDead) continue;
                if (c.CurrentHealth < c.MaxHealth * 0.6f)
                {
                    allyPos = posRO.ValueRO;
                    return true;
                }
            }
            return false;
        }

        private bool TryGetEntityPosition(int entityIndex, int dungeonLevel, out PositionComponent pos)
        {
            foreach (var (posRO, entity) in SystemAPI.Query<RefRO<PositionComponent>>().WithEntityAccess())
            {
                if (entity.Index == entityIndex && posRO.ValueRO.DungeonLevel == dungeonLevel)
                {
                    pos = posRO.ValueRO;
                    return true;
                }
            }
            pos = default;
            return false;
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

        protected override void OnCreate()
        {
            base.OnCreate();
            _eventBus = EventBus.Instance;
            _spatialGrid = SpatialHashGrid.Instance;
            _entityCache = EntityIndexCache.Instance;
        }

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

        public LootSystem()
        {
            _rng = new DeterministicRNG();
            _eventBus = EventBus.Instance;
        }

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            foreach (var (combatRO, lootRO) in SystemAPI.Query<RefRO<CombatComponent>, RefRO<LootTableComponent>>())
            {
                var combat = combatRO.ValueRO;
                var loot = lootRO.ValueRO;

                if (combat.IsDead && loot.DropOnDeath)
                {
                    int goldDrop = _rng.RollDice(loot.GoldDropMax - loot.GoldDropMin + 1) + loot.GoldDropMin;

                    _eventBus?.Publish(new LootGrantedEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)UnityEngine.Time.frameCount,
                        Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                        RecipientEntityId = combat.CombatSessionId,
                        LootTableId = loot.LootTableId,
                        GoldAmount = goldDrop
                    });
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

        protected override void OnCreate()
        {
            base.OnCreate();
            _eventBus = EventBus.Instance;
        }

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            foreach (var (expRW, combatRO) in SystemAPI.Query<RefRW<ExperienceComponent>, RefRO<CombatComponent>>())
            {
                ref var exp = ref expRW.ValueRW;
                var combat = combatRO.ValueRO;

                if (combat.IsInCombat && !combat.IsDead)
                {
                    int previousLevel = exp.Level;
                    exp.CurrentXP += 50;

                    while (exp.CanLevelUp())
                    {
                        exp.LevelUp();

                        _eventBus?.Publish(new LevelUpEventData
                        {
                            EventId = _eventBus.GetNextEventId(),
                            FrameNumber = (uint)UnityEngine.Time.frameCount,
                            Timestamp = (uint)UnityEngine.Time.frameCount / 60f,
                            EntityId = combat.CombatSessionId,
                            PreviousLevel = previousLevel,
                            NewLevel = exp.Level,
                            RemainingXP = exp.CurrentXP,
                            XPToNextLevel = exp.XPToNextLevel
                        });

                        previousLevel = exp.Level;
                    }
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
            _eventBus = EventBus.Instance;
        }

        public void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null)
                return;

            var explorationQuery = SystemAPI.QueryBuilder()
                .WithAll<PositionComponent, VisionComponent, CombatComponent>()
                .Build();
            using var explorationEntities = explorationQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var explorationPositions = explorationQuery.ToComponentDataArray<PositionComponent>(Unity.Collections.Allocator.Temp);
            using var explorationVisions = explorationQuery.ToComponentDataArray<VisionComponent>(Unity.Collections.Allocator.Temp);
            using var explorationCombats = explorationQuery.ToComponentDataArray<CombatComponent>(Unity.Collections.Allocator.Temp);

            for (int _ei = 0; _ei < explorationEntities.Length; _ei++)
            {
                var entity = explorationEntities[_ei];
                var pos = explorationPositions[_ei];
                var vision = explorationVisions[_ei];
                var combat = explorationCombats[_ei];

                if (!vision.IsPlayerControlled || combat.IsDead || combat.IsInCombat)
                    continue;

                int encounter = _rng.RollDice(100);
                if (encounter >= 15)
                    continue;

                if (TryFindEncounterTarget(entity, pos, out var targetEntity))
                    CombatEncounterUtility.StartCombat(EntityManager, entity, targetEntity, _nextCombatSessionId++, (uint)UnityEngine.Time.frameCount);
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
