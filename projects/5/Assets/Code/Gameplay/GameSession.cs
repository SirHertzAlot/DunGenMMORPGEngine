using System;
using System.Collections.Generic;
using System.IO;
using DunGen.Config;
using DunGen.ECS.Combat;
using DunGen.ECS.Components;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using DunGen.ECS.Generation;
using DunGen.ECS.Models;
using DunGen.ECS.Systems.Combat;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Simulation.RNG;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Gameplay
{
    /// <summary>
    /// Complete game session - start dungeon, handle turns, manage progression.
    /// This is the main entry point for a playable MVP session.
    /// </summary>
    public class GameSession : IDisposable
    {
        public int CurrentLevel { get; set; } = 1;
        public int PlayerEntityId { get; set; } = 1;
        public int TurnCount { get; set; }
        public bool IsGameOver { get; set; }
        public string GameOverReason { get; set; } = "";

        private const int DefaultLevelWidth = 80;
        private const int DefaultLevelHeight = 24;
        private const int DefaultPlayerX = 40;
        private const int DefaultPlayerY = 12;

        private readonly EventBus _eventBus;
        private readonly EntityManager _entityManager;
        private readonly World _ecsWorld;
        private readonly List<Entity> _sessionEntities = new();
        private readonly int _seed;
        private readonly DeterministicRNG _rng;
        private readonly EventLog _eventLog;
        private readonly Action _eventLogSubscription;
        private readonly VisualSpawnPoolConfig _visualSpawnConfig;
        private readonly ModelAssetManifest _visualManifest;
        private readonly VisualSpawnPool _visualSpawnPool;
        private readonly Dictionary<int, PooledVisualBinding> _entityVisuals = new();
        private readonly GameObject _visualPoolRoot;
        private SimpleDungeonGenerator _generator;
        private int _nextCombatSessionId = 100;
        private readonly EntityIndexCache _entityCache;
        private readonly SpatialHashGrid _spatialGrid;

        public GameSession(int seed = 12345, VisualSpawnPoolConfig visualSpawnConfig = null, ModelAssetManifest visualManifest = null)
        {
            _seed = seed;
            _rng = new DeterministicRNG(seed);
            _generator = new SimpleDungeonGenerator(seed);
            _eventBus = EventBus.Instance;
            _eventLog = new EventLog();
            _eventLogSubscription = _eventBus.SubscribeAll((evt, _) => _eventLog.RecordPublishedEvent(evt));
            _ecsWorld = World.DefaultGameObjectInjectionWorld ?? new World("DunGenGameSession");
            _entityManager = _ecsWorld.EntityManager;
            _entityCache = EntityIndexCache.Instance;
            _spatialGrid = SpatialHashGrid.Instance;
            _visualSpawnConfig = visualSpawnConfig ?? LoadDefaultVisualSpawnConfig();
            _visualManifest = visualManifest ?? PolygonFantasyHeroManifest.LoadFromResources();
            _visualPoolRoot = new GameObject("GameSession Visual Spawn Pool");
            _visualSpawnPool = new VisualSpawnPool(_visualPoolRoot.transform);
        }

        /// <summary>Initialize a new game session.</summary>
        public void StartGame()
        {
            _eventLog.Initialize((ulong)_seed);
            CleanupSessionEntities();

            CurrentLevel = 1;
            TurnCount = 0;
            IsGameOver = false;
            GameOverReason = "";
            _nextCombatSessionId = 100;
            _rng.SetSeed((uint)_seed);
            _generator = new SimpleDungeonGenerator(_seed);
            PrewarmVisualPool();

            Debug.Log("Starting new game session...");
            _eventBus.Publish(new GameSessionStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                Seed = (ulong)_seed,
                LevelNumber = CurrentLevel
            });

            CreatePlayer();
            GenerateDungeonLevel(CurrentLevel);
            CreateEnemies(3);

            Debug.Log("Game session started.");
        }

        /// <summary>Create the player entity.</summary>
        private void CreatePlayer()
        {
            var playerEntity = _entityManager.CreateEntity(
                typeof(CombatComponent),
                typeof(CombatStatsComponent),
                typeof(InitiativeComponent),
                typeof(CombatRoundComponent),
                typeof(ActionQueueComponent),
                typeof(ActionCostComponent),
                typeof(ConditionComponent),
                typeof(PositionComponent),
                typeof(MovementComponent),
                typeof(VisionComponent),
                typeof(ExperienceComponent),
                typeof(CurrencyComponent),
                typeof(VisualModelComponent));

            _entityManager.SetComponentData(playerEntity, new CombatComponent
            {
                CurrentHealth = 100,
                MaxHealth = 100,
                ArmorClass = 12,
                IsInCombat = false,
                CombatSessionId = 1
            });

            _entityManager.SetComponentData(playerEntity, new CombatStatsComponent
            {
                StrengthModifier = 2,
                DexterityModifier = 1,
                ConstitutionModifier = 2,
                IntelligenceModifier = 0,
                WisdomModifier = 1,
                CharismaModifier = 0,
                ProficiencyBonus = 2,
                CurrentMana = 50,
                MaxMana = 50,
                ActionsRemaining = 1
            });

            _entityManager.SetComponentData(playerEntity, new InitiativeComponent
            {
                DexModifier = 1,
                InitiativeScore = 0,
                TurnOrder = 0
            });

            _entityManager.SetComponentData(playerEntity, new CombatRoundComponent
            {
                ActiveCombatantId = playerEntity.Index,
                RoundNumber = 0,
                TotalParticipants = 1,
                CurrentTurnIndex = 0,
                CombatPhase = 0
            });

            var playerActionCosts = new ActionCostComponent();
            playerActionCosts.ResetForNewTurn();
            _entityManager.SetComponentData(playerEntity, playerActionCosts);
            _entityManager.SetComponentData(playerEntity, new ConditionComponent());

            _entityManager.SetComponentData(playerEntity, new PositionComponent
            {
                X = DefaultPlayerX,
                Y = DefaultPlayerY,
                DungeonLevel = CurrentLevel
            });

            _entityManager.SetComponentData(playerEntity, new MovementComponent
            {
                MovementSpeed = 5,
                TilesMovedThisTurn = 0
            });

            _entityManager.SetComponentData(playerEntity, new VisionComponent
            {
                VisionRange = 10,
                IsPlayerControlled = true
            });

            _entityManager.SetComponentData(playerEntity, new ExperienceComponent
            {
                CurrentXP = 0,
                Level = 1,
                XPToNextLevel = 100
            });

            _entityManager.SetComponentData(playerEntity, new CurrencyComponent
            {
                Gold = 0
            });

            _entityManager.SetComponentData(playerEntity, VisualModelCatalog.CreateHero());

            TrackEntity(playerEntity);
            PlayerEntityId = playerEntity.Index;

            // Register in cache for O(1) lookups
            _entityCache.RegisterPlayer(playerEntity);
            _spatialGrid.UpdatePosition(playerEntity.Index, DefaultPlayerX, DefaultPlayerY, CurrentLevel);
            PublishEntityCreated(playerEntity, "Player", "Hero");
            PublishEntitySpawned(playerEntity, "Player", "SessionStart", DefaultPlayerX, DefaultPlayerY, CurrentLevel);
            AttachPooledVisual(playerEntity, CharacterArchetype.Hero, DefaultPlayerX, DefaultPlayerY, CurrentLevel);

            Debug.Log($"Player created (Entity: {PlayerEntityId})");
        }

        /// <summary>Generate a dungeon level.</summary>
        private void GenerateDungeonLevel(int level)
        {
            var levelEntity = _entityManager.CreateEntity(typeof(DungeonLevelComponent));

            _entityManager.SetComponentData(levelEntity, new DungeonLevelComponent
            {
                LevelNumber = level,
                Width = DefaultLevelWidth,
                Height = DefaultLevelHeight,
                Seed = _seed + level,
                EnemyCount = 5 + level,
                LootCount = 3 + level,
                IsGenerated = true
            });

            TrackEntity(levelEntity);
            PublishEntityCreated(levelEntity, "DungeonLevel", $"Level {level}");

            int tileCount = 0;
            foreach (var tile in _generator.GenerateTiles(level, DefaultLevelWidth, DefaultLevelHeight))
            {
                var tileEntity = _entityManager.CreateEntity(typeof(DungeonTile));
                _entityManager.SetComponentData(tileEntity, tile);
                TrackEntity(tileEntity);
                tileCount++;
            }

            _eventBus.Publish(new DungeonLevelGeneratedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                LevelNumber = level,
                Width = DefaultLevelWidth,
                Height = DefaultLevelHeight,
                TileCount = tileCount,
                EnemyBudget = 5 + level,
                LootBudget = 3 + level,
                Seed = _seed + level
            });

            Debug.Log($"Dungeon level {level} generated ({DefaultLevelWidth}x{DefaultLevelHeight}).");
        }

        /// <summary>Create enemy entities.</summary>
        private void CreateEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var enemyEntity = _entityManager.CreateEntity(
                    typeof(CombatComponent),
                    typeof(CombatStatsComponent),
                    typeof(InitiativeComponent),
                    typeof(CombatRoundComponent),
                    typeof(ActionQueueComponent),
                    typeof(ActionCostComponent),
                    typeof(ConditionComponent),
                    typeof(PositionComponent),
                    typeof(MovementComponent),
                    typeof(LootTableComponent),
                    typeof(VisualModelComponent));

                _entityManager.SetComponentData(enemyEntity, new CombatComponent
                {
                    CurrentHealth = 50,
                    MaxHealth = 50,
                    ArmorClass = 11,
                    IsInCombat = false,
                    CombatSessionId = i + 2
                });

                _entityManager.SetComponentData(enemyEntity, new CombatStatsComponent
                {
                    StrengthModifier = 1,
                    DexterityModifier = 0,
                    ConstitutionModifier = 1,
                    IntelligenceModifier = -1,
                    ProficiencyBonus = 2,
                    ActionsRemaining = 1
                });

                _entityManager.SetComponentData(enemyEntity, new InitiativeComponent
                {
                    DexModifier = 0,
                    InitiativeScore = 0,
                    TurnOrder = 0
                });

                _entityManager.SetComponentData(enemyEntity, new CombatRoundComponent
                {
                    ActiveCombatantId = enemyEntity.Index,
                    RoundNumber = 0,
                    TotalParticipants = 1,
                    CurrentTurnIndex = 0,
                    CombatPhase = 0
                });

                var enemyActionCosts = new ActionCostComponent();
                enemyActionCosts.ResetForNewTurn();
                _entityManager.SetComponentData(enemyEntity, enemyActionCosts);
                _entityManager.SetComponentData(enemyEntity, new ConditionComponent());

                var (spawnX, spawnY) = GetEnemySpawnPosition(i);
                _entityManager.SetComponentData(enemyEntity, new PositionComponent
                {
                    X = spawnX,
                    Y = spawnY,
                    DungeonLevel = CurrentLevel
                });

                _entityManager.SetComponentData(enemyEntity, new MovementComponent
                {
                    MovementSpeed = 3,
                    TilesMovedThisTurn = 0
                });

                _entityManager.SetComponentData(enemyEntity, new LootTableComponent
                {
                    GoldDropMin = 10,
                    GoldDropMax = 50,
                    LootTableId = 1,
                    DropOnDeath = true
                });

                var enemyKey = _visualSpawnConfig.GetEnemyKeyForSpawn(i);
                var enemyArchetype = ResolveEnemyVisualArchetype(enemyKey, i);
                _entityManager.SetComponentData(enemyEntity, VisualModelCatalog.Create(enemyArchetype));

                TrackEntity(enemyEntity);

                // Register in cache for O(1) lookups
                _entityCache.Register(enemyEntity, i + 2);
                _spatialGrid.UpdatePosition(enemyEntity.Index, spawnX, spawnY, CurrentLevel);
                PublishEntityCreated(enemyEntity, "Enemy", enemyKey);
                PublishEntitySpawned(enemyEntity, "Enemy", "DungeonPopulation", spawnX, spawnY, CurrentLevel);
                AttachPooledVisual(enemyEntity, enemyArchetype, spawnX, spawnY, CurrentLevel);
            }

            Debug.Log($"Created {count} enemies.");
        }

        /// <summary>Execute one full game turn.</summary>
        public void ExecuteTurn()
        {
            ExecuteTurnWithAutoplay();
        }

        private void ExecuteTurnWithAutoplay()
        {
            TurnCount++;
            _eventBus.Publish(new GameTurnStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                TurnNumber = TurnCount,
                LevelNumber = CurrentLevel,
                LivingEnemyCount = GetLivingEnemyCount()
            });
            ResetMovementForNewTurn();
            ResolveExplorationStep();
            ResolveCombatStep();
            ResolveRewardsAndProgression();
            CheckGameState();
            _eventBus.Publish(new GameTurnCompletedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                TurnNumber = TurnCount,
                LevelNumber = CurrentLevel,
                LivingEnemyCount = GetLivingEnemyCount(),
                IsGameOver = IsGameOver
            });
            _eventLog.AdvanceFrame();
        }

        /// <summary>Execute one player-driven turn. Used by the Unity client boundary.</summary>
        public bool TryExecutePlayerCommand(PlayerTurnCommand command, out string resultMessage)
        {
            resultMessage = "";

            if (!ValidatePlayerCommand(command, out resultMessage))
                return false;

            TurnCount++;
            _eventBus.Publish(new GameTurnStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                TurnNumber = TurnCount,
                LevelNumber = CurrentLevel,
                LivingEnemyCount = GetLivingEnemyCount()
            });

            ResetMovementForNewTurn();
            ApplyPlayerCommand(command);

            if (TryGetPlayerEntity(out var playerEntity))
            {
                MoveEnemies(playerEntity);
                StartNearbyCombats(playerEntity);
            }

            ResolveCombatStep();
            ResolveRewardsAndProgression();
            CheckGameState();
            _eventBus.Publish(new GameTurnCompletedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                TurnNumber = TurnCount,
                LevelNumber = CurrentLevel,
                LivingEnemyCount = GetLivingEnemyCount(),
                IsGameOver = IsGameOver
            });
            _eventLog.AdvanceFrame();

            resultMessage = command.Type switch
            {
                PlayerTurnCommandType.Move => $"Moved player by ({command.DeltaX}, {command.DeltaY}).",
                PlayerTurnCommandType.AttackNearest => "Resolved nearest attack opportunity.",
                _ => "Waited."
            };
            return true;
        }

        private void ResetMovementForNewTurn()
        {
            var query = _entityManager.CreateEntityQuery(typeof(MovementComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var movement = _entityManager.GetComponentData<MovementComponent>(entities[i]);
                movement.TilesMovedThisTurn = 0;
                _entityManager.SetComponentData(entities[i], movement);
            }
        }

        private void ResolveExplorationStep()
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            if (playerCombat.IsDead)
                return;

            if (!playerCombat.IsInCombat)
                MovePlayerTowardsNearestEnemy(playerEntity);

            MoveEnemies(playerEntity);
            StartNearbyCombats(playerEntity);
        }

        private void ResolveCombatStep()
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            if (!playerCombat.IsInCombat || playerCombat.IsDead)
                return;

            var participants = GetCombatParticipants(playerCombat.CombatSessionId);
            if (participants.Count == 0)
                return;

            participants.Sort(CompareByInitiativeDescending);
            var orchestrator = new CombatOrchestrator((uint)(_seed + TurnCount + playerCombat.CombatSessionId), _eventBus);

            foreach (var actor in participants)
            {
                if (!_entityManager.Exists(actor))
                    continue;

                var actorCombat = _entityManager.GetComponentData<CombatComponent>(actor);
                if (actorCombat.IsDead || !actorCombat.IsInCombat)
                    continue;

                var target = actor == playerEntity
                    ? FindNearestHostile(actor, playerCombat.CombatSessionId)
                    : playerEntity;

                if (target == Entity.Null || !_entityManager.Exists(target))
                    continue;

                var targetCombat = _entityManager.GetComponentData<CombatComponent>(target);
                if (targetCombat.IsDead)
                    continue;

                var actorRound = _entityManager.GetComponentData<CombatRoundComponent>(actor);
                actorRound.RoundNumber = Mathf.Max(1, actorRound.RoundNumber);
                actorRound.CurrentTurnIndex++;
                actorRound.ActionsThisRound++;
                actorRound.ActiveCombatantId = actor.Index;
                _entityManager.SetComponentData(actor, actorRound);

                int damage = orchestrator.ExecuteAttack(
                    actor.Index,
                    target.Index,
                    GetAttackModifier(actor),
                    targetCombat.ArmorClass,
                    actor == playerEntity ? "Player Attack" : "Enemy Attack",
                    "1d8");

                if (damage > 0)
                    ApplyDamage(target, actor.Index, damage, actor == playerEntity ? "Player Attack" : "Enemy Attack");
            }

            FinalizeCombatState(playerCombat.CombatSessionId);
        }

        private void ResolveRewardsAndProgression()
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            AwardLootAndExperience(playerEntity);

            if (GetLivingEnemyCount() > 0)
                return;

            CurrentLevel++;
            CleanupNonPlayerEntities(playerEntity);
            GenerateDungeonLevel(CurrentLevel);
            RepositionPlayerForNewLevel(playerEntity);
            CreateEnemies(2 + CurrentLevel);
        }

        /// <summary>Check if game should end.</summary>
        private void CheckGameState()
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            if (playerCombat.IsDead)
            {
                IsGameOver = true;
                GameOverReason = "Player defeated!";
                Debug.Log("GAME OVER: Player defeated!");
            }
        }

        /// <summary>Get current game state for display.</summary>
        public GameState GetGameState()
        {
            if (!TryGetPlayerEntity(out var playerEntity))
            {
                return new GameState
                {
                    CurrentLevel = CurrentLevel,
                    TurnCount = TurnCount
                };
            }

            var playerPos = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            var playerExp = _entityManager.GetComponentData<ExperienceComponent>(playerEntity);
            var playerCurrency = _entityManager.GetComponentData<CurrencyComponent>(playerEntity);

            return new GameState
            {
                PlayerX = playerPos.X,
                PlayerY = playerPos.Y,
                PlayerHealth = playerCombat.CurrentHealth,
                PlayerMaxHealth = playerCombat.MaxHealth,
                PlayerLevel = playerExp.Level,
                PlayerXP = playerExp.CurrentXP,
                PlayerGold = playerCurrency.Gold,
                CurrentLevel = CurrentLevel,
                TurnCount = TurnCount
            };
        }

        private bool TryGetPlayerEntity(out Entity playerEntity)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            return _entityCache.TryGetPlayerEntity(out playerEntity);
        }

        private bool ValidatePlayerCommand(PlayerTurnCommand command, out string failureReason)
        {
            failureReason = "";

            if (IsGameOver)
            {
                failureReason = string.IsNullOrWhiteSpace(GameOverReason) ? "Game is over." : GameOverReason;
                return false;
            }

            if (!TryGetPlayerEntity(out var playerEntity) || !_entityManager.Exists(playerEntity))
            {
                failureReason = "Player entity is not available.";
                return false;
            }

            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            if (playerCombat.IsDead)
            {
                failureReason = "Player is defeated.";
                return false;
            }

            if (command.Type == PlayerTurnCommandType.Wait || command.Type == PlayerTurnCommandType.AttackNearest)
                return true;

            if (command.Type != PlayerTurnCommandType.Move)
            {
                failureReason = $"Unsupported player command '{command.Type}'.";
                return false;
            }

            if (Mathf.Abs(command.DeltaX) + Mathf.Abs(command.DeltaY) != 1)
            {
                failureReason = "Move commands must target one cardinal tile.";
                return false;
            }

            if (playerCombat.IsInCombat)
            {
                failureReason = "Player cannot move while in combat.";
                return false;
            }

            var playerPosition = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            int nextX = playerPosition.X + command.DeltaX;
            int nextY = playerPosition.Y + command.DeltaY;
            if (!_generator.IsWalkable(nextX, nextY, DefaultLevelWidth, DefaultLevelHeight))
            {
                failureReason = "Target tile is not walkable.";
                return false;
            }

            if (IsOccupied(nextX, nextY))
            {
                failureReason = "Target tile is occupied.";
                return false;
            }

            return true;
        }

        private void ApplyPlayerCommand(PlayerTurnCommand command)
        {
            if (command.Type != PlayerTurnCommandType.Move)
                return;

            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var movement = _entityManager.GetComponentData<MovementComponent>(playerEntity);
            int fromX = position.X;
            int fromY = position.Y;
            position.X += command.DeltaX;
            position.Y += command.DeltaY;
            movement.TilesMovedThisTurn++;

            _entityManager.SetComponentData(playerEntity, position);
            _entityManager.SetComponentData(playerEntity, movement);
            _spatialGrid.UpdatePosition(playerEntity.Index, position.X, position.Y, position.DungeonLevel);
            PublishEntityMoved(playerEntity, fromX, fromY, position.X, position.Y);
        }

        private void MovePlayerTowardsNearestEnemy(Entity playerEntity)
        {
            if (!_entityManager.HasComponent<PositionComponent>(playerEntity))
                return;

            var enemyEntity = FindNearestHostile(playerEntity, -1);
            if (enemyEntity == Entity.Null || !_entityManager.HasComponent<PositionComponent>(enemyEntity))
                return;

            var playerPosition = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var enemyPosition = _entityManager.GetComponentData<PositionComponent>(enemyEntity);
            MoveEntityTowards(playerEntity, playerPosition, enemyPosition);
        }

        private void MoveEnemies(Entity playerEntity)
        {
            if (!_entityManager.HasComponent<PositionComponent>(playerEntity))
                return;

            var playerPosition = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var query = _entityManager.CreateEntityQuery(
                typeof(CombatComponent),
                typeof(PositionComponent),
                typeof(MovementComponent),
                typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var enemyEntity = entities[i];
                var combat = _entityManager.GetComponentData<CombatComponent>(enemyEntity);
                if (combat.IsDead || combat.IsInCombat)
                    continue;

                var enemyPosition = _entityManager.GetComponentData<PositionComponent>(enemyEntity);
                if (enemyPosition.DungeonLevel != playerPosition.DungeonLevel)
                    continue;

                MoveEntityTowards(enemyEntity, enemyPosition, playerPosition);
            }
        }

        private void MoveEntityTowards(Entity entity, PositionComponent source, PositionComponent target)
        {
            if (!_entityManager.HasComponent<MovementComponent>(entity))
                return;

            var movement = _entityManager.GetComponentData<MovementComponent>(entity);
            if (movement.TilesMovedThisTurn >= movement.MovementSpeed)
                return;

            var (dx, dy) = _generator.GetMoveTowards(source.X, source.Y, target.X, target.Y);
            if (dx == 0 && dy == 0)
                return;

            int nextX = source.X + dx;
            int nextY = source.Y + dy;
            if (!_generator.IsWalkable(nextX, nextY, DefaultLevelWidth, DefaultLevelHeight))
                return;

            int fromX = source.X;
            int fromY = source.Y;
            source.X = nextX;
            source.Y = nextY;
            movement.TilesMovedThisTurn++;

            _entityManager.SetComponentData(entity, source);
            _entityManager.SetComponentData(entity, movement);
            _spatialGrid.UpdatePosition(entity.Index, source.X, source.Y, source.DungeonLevel);
            PublishEntityMoved(entity, fromX, fromY, source.X, source.Y);
        }

        private void StartNearbyCombats(Entity playerEntity)
        {
            if (!_entityManager.HasComponent<PositionComponent>(playerEntity))
                return;

            var playerPosition = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            if (playerCombat.IsDead || playerCombat.IsInCombat)
                return;

            var query = _entityManager.CreateEntityQuery(
                typeof(CombatComponent),
                typeof(PositionComponent),
                typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var enemyEntity = entities[i];
                var combat = _entityManager.GetComponentData<CombatComponent>(enemyEntity);
                if (combat.IsDead || combat.IsInCombat)
                    continue;

                var enemyPosition = _entityManager.GetComponentData<PositionComponent>(enemyEntity);
                if (enemyPosition.DungeonLevel != playerPosition.DungeonLevel)
                    continue;

                int distance = Mathf.Abs(enemyPosition.X - playerPosition.X) + Mathf.Abs(enemyPosition.Y - playerPosition.Y);
                if (distance <= 1)
                {
                    StartCombatSession(playerEntity, enemyEntity);
                    return;
                }
            }
        }

        private void StartCombatSession(Entity playerEntity, Entity enemyEntity)
        {
            int combatSessionId = _nextCombatSessionId++;
            int playerInitiative = RollInitiative(playerEntity);
            int enemyInitiative = RollInitiative(enemyEntity);
            int[] initiativeOrder = playerInitiative >= enemyInitiative
                ? new[] { playerEntity.Index, enemyEntity.Index }
                : new[] { enemyEntity.Index, playerEntity.Index };

            SetCombatState(playerEntity, combatSessionId, playerInitiative, initiativeOrder[0] == playerEntity.Index ? 0 : 1);
            SetCombatState(enemyEntity, combatSessionId, enemyInitiative, initiativeOrder[0] == enemyEntity.Index ? 0 : 1);

            _eventBus.Publish(new CombatStartedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)Time.frameCount,
                Timestamp = (uint)Time.frameCount / 60f,
                ParticipantEntityIds = new[] { playerEntity.Index, enemyEntity.Index },
                InitiativeOrder = initiativeOrder,
                CombatSessionId = combatSessionId
            });
        }

        private void SetCombatState(Entity entity, int combatSessionId, int initiativeScore, int turnOrder)
        {
            _entityCache.Register(entity, combatSessionId);
            var combat = _entityManager.GetComponentData<CombatComponent>(entity);
            combat.IsInCombat = true;
            combat.CombatSessionId = combatSessionId;
            combat.CombatSeed = (uint)(_seed + combatSessionId);
            combat.CurrentRound = 1;
            combat.HasActedThisRound = false;
            _entityManager.SetComponentData(entity, combat);

            var initiative = _entityManager.GetComponentData<InitiativeComponent>(entity);
            initiative.D20Roll = initiativeScore - initiative.DexModifier;
            initiative.InitiativeScore = initiativeScore;
            initiative.TurnOrder = turnOrder;
            _entityManager.SetComponentData(entity, initiative);

            var round = _entityManager.GetComponentData<CombatRoundComponent>(entity);
            round.ActiveCombatantId = entity.Index;
            round.RoundNumber = 1;
            round.TotalParticipants = 2;
            round.CurrentTurnIndex = 0;
            round.CombatPhase = 1;
            round.ActionsThisRound = 0;
            round.DamageThisRound = 0;
            _entityManager.SetComponentData(entity, round);
        }

        private int RollInitiative(Entity entity)
        {
            var initiative = _entityManager.GetComponentData<InitiativeComponent>(entity);
            int d20 = _rng.RollD20();
            return d20 + initiative.DexModifier;
        }

        private List<Entity> GetCombatParticipants(int combatSessionId)
        {
            var result = new List<Entity>();
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(InitiativeComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (combat.CombatSessionId == combatSessionId)
                    result.Add(entities[i]);
            }

            return result;
        }

        private int CompareByInitiativeDescending(Entity left, Entity right)
        {
            var leftInitiative = _entityManager.GetComponentData<InitiativeComponent>(left);
            var rightInitiative = _entityManager.GetComponentData<InitiativeComponent>(right);
            int scoreCompare = rightInitiative.InitiativeScore.CompareTo(leftInitiative.InitiativeScore);
            return scoreCompare != 0 ? scoreCompare : left.Index.CompareTo(right.Index);
        }

        private Entity FindNearestHostile(Entity sourceEntity, int combatSessionId)
        {
            if (!_entityManager.HasComponent<PositionComponent>(sourceEntity))
                return Entity.Null;

            var sourcePosition = _entityManager.GetComponentData<PositionComponent>(sourceEntity);
            var query = _entityManager.CreateEntityQuery(
                typeof(CombatComponent),
                typeof(PositionComponent),
                typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            Entity nearest = Entity.Null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == sourceEntity)
                    continue;

                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (combat.IsDead)
                    continue;

                if (combatSessionId >= 0 && combat.CombatSessionId != combatSessionId)
                    continue;

                var position = _entityManager.GetComponentData<PositionComponent>(entities[i]);
                if (position.DungeonLevel != sourcePosition.DungeonLevel)
                    continue;

                int distance = Mathf.Abs(position.X - sourcePosition.X) + Mathf.Abs(position.Y - sourcePosition.Y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = entities[i];
                }
            }

            return nearest;
        }

        private int GetAttackModifier(Entity entity)
        {
            if (!_entityManager.HasComponent<CombatStatsComponent>(entity))
                return 0;

            var stats = _entityManager.GetComponentData<CombatStatsComponent>(entity);
            return stats.StrengthModifier + stats.ProficiencyBonus;
        }

        private void ApplyDamage(Entity target, int attackerId, int damage, string source)
        {
            var targetCombat = _entityManager.GetComponentData<CombatComponent>(target);
            targetCombat.CurrentHealth = Mathf.Max(0, targetCombat.CurrentHealth - damage);
            if (targetCombat.IsDead)
                targetCombat.IsInCombat = false;
            _entityManager.SetComponentData(target, targetCombat);

            if (!targetCombat.IsDead)
                return;

            _eventBus.Publish(new DeathEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = (uint)Time.frameCount,
                Timestamp = (uint)Time.frameCount / 60f,
                DeceasedEntityId = target.Index,
                KillerEntityId = attackerId,
                SurvivingCombatants = GetLivingCombatants(targetCombat.CombatSessionId),
                CauseOfDeath = source
            });
        }

        private int[] GetLivingCombatants(int combatSessionId)
        {
            var participants = GetCombatParticipants(combatSessionId);
            var living = new List<int>(participants.Count);

            for (int i = 0; i < participants.Count; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(participants[i]);
                if (!combat.IsDead)
                    living.Add(participants[i].Index);
            }

            return living.ToArray();
        }

        private void FinalizeCombatState(int combatSessionId)
        {
            var participants = GetCombatParticipants(combatSessionId);
            int livingCount = 0;

            for (int i = 0; i < participants.Count; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(participants[i]);
                if (!combat.IsDead)
                    livingCount++;
            }

            if (livingCount > 1)
                return;

            foreach (var participant in participants)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(participant);
                combat.IsInCombat = false;
                _entityManager.SetComponentData(participant, combat);
            }
        }

        private void AwardLootAndExperience(Entity playerEntity)
        {
            var currency = _entityManager.GetComponentData<CurrencyComponent>(playerEntity);
            var experience = _entityManager.GetComponentData<ExperienceComponent>(playerEntity);
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var enemyEntity = entities[i];
                var combat = _entityManager.GetComponentData<CombatComponent>(enemyEntity);
                var loot = _entityManager.GetComponentData<LootTableComponent>(enemyEntity);
                if (!combat.IsDead || !loot.DropOnDeath)
                    continue;

                int minGold = Mathf.Min(loot.GoldDropMin, loot.GoldDropMax);
                int maxGold = Mathf.Max(loot.GoldDropMin, loot.GoldDropMax);
                int goldAward = minGold + _rng.RollDice(maxGold - minGold + 1) - 1;
                currency.AddGold(goldAward);
                experience.CurrentXP += 50;

                _eventBus.Publish(new LootGrantedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = (uint)Time.frameCount,
                    Timestamp = (uint)Time.frameCount / 60f,
                    RecipientEntityId = playerEntity.Index,
                    LootTableId = loot.LootTableId,
                    GoldAmount = goldAward
                });

                while (experience.CanLevelUp())
                {
                    int previousLevel = experience.Level;
                    experience.LevelUp();
                    _eventBus.Publish(new LevelUpEventData
                    {
                        EventId = _eventBus.GetNextEventId(),
                        FrameNumber = (uint)Time.frameCount,
                        Timestamp = (uint)Time.frameCount / 60f,
                        EntityId = playerEntity.Index,
                        PreviousLevel = previousLevel,
                        NewLevel = experience.Level,
                        RemainingXP = experience.CurrentXP,
                        XPToNextLevel = experience.XPToNextLevel
                    });
                }

                loot.DropOnDeath = false;
                _entityManager.SetComponentData(enemyEntity, loot);
            }

            _entityManager.SetComponentData(playerEntity, currency);
            _entityManager.SetComponentData(playerEntity, experience);
        }

        private int GetLivingEnemyCount()
        {
            int count = 0;
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (!combat.IsDead)
                    count++;
            }

            return count;
        }

        public int GetLivingEnemyCountForClient() => GetLivingEnemyCount();

        private void RepositionPlayerForNewLevel(Entity playerEntity)
        {
            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            int fromX = position.X;
            int fromY = position.Y;
            position.X = DefaultPlayerX;
            position.Y = DefaultPlayerY;
            position.DungeonLevel = CurrentLevel;
            _entityManager.SetComponentData(playerEntity, position);
            _spatialGrid.UpdatePosition(playerEntity.Index, position.X, position.Y, position.DungeonLevel);
            PublishEntityMoved(playerEntity, fromX, fromY, position.X, position.Y);

            var combat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            combat.IsInCombat = false;
            combat.CombatSessionId = 1;
            combat.CurrentRound = 0;
            combat.HasActedThisRound = false;
            _entityManager.SetComponentData(playerEntity, combat);
        }

        private void CleanupNonPlayerEntities(Entity playerEntity)
        {
            for (int i = _sessionEntities.Count - 1; i >= 0; i--)
            {
                var entity = _sessionEntities[i];
                if (entity == playerEntity)
                    continue;

                if (_entityManager.Exists(entity))
                {
                    ReturnVisualForEntity(entity);
                    _entityCache.Unregister(entity);
                    _spatialGrid.Remove(entity.Index);
                    PublishEntityDestroyed(entity, InferEntityType(entity), "LevelTransition");
                    _entityManager.DestroyEntity(entity);
                }

                _sessionEntities.RemoveAt(i);
            }
        }

        private void CleanupSessionEntities()
        {
            for (int i = _sessionEntities.Count - 1; i >= 0; i--)
            {
                if (_entityManager.Exists(_sessionEntities[i]))
                {
                    var entity = _sessionEntities[i];
                    ReturnVisualForEntity(entity);
                    _entityCache.Unregister(entity);
                    _spatialGrid.Remove(entity.Index);
                    PublishEntityDestroyed(entity, InferEntityType(entity), "SessionCleanup");
                    _entityManager.DestroyEntity(entity);
                }
            }

            _sessionEntities.Clear();

            // Clear caches for new session
            _entityCache.Clear();
            _spatialGrid.Clear();
        }

        private void TrackEntity(Entity entity)
        {
            _sessionEntities.Add(entity);
        }

        private VisualSpawnPoolConfig LoadDefaultVisualSpawnConfig()
        {
            try
            {
                return VisualSpawnConfigReader.Load(new ConfigLoader());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Visual spawn config unavailable; using defaults. {ex.Message}");
                return new VisualSpawnPoolConfig();
            }
        }

        private void PrewarmVisualPool()
        {
            if (_visualManifest == null)
                return;

            foreach (CharacterArchetype archetype in Enum.GetValues(typeof(CharacterArchetype)))
            {
                int count = _visualSpawnConfig.GetPrewarmCount(archetype);
                if (count <= 0)
                    continue;

                _visualSpawnPool.Prewarm(_visualManifest, new[] { archetype }, count);
            }
        }

        public CharacterArchetype ResolveEnemyVisualArchetype(string enemyKey, int enemyIndex)
        {
            var fallback = ((CurrentLevel + enemyIndex) % 4) switch
            {
                0 => CharacterArchetype.Bandit,
                1 => CharacterArchetype.Rogue,
                2 => CharacterArchetype.Barbarian,
                _ => CharacterArchetype.Knight
            };

            return _visualSpawnConfig.ResolveEnemyArchetype(enemyKey, fallback);
        }

        private void AttachPooledVisual(Entity entity, CharacterArchetype archetype, int x, int y, int level)
        {
            if (!_visualSpawnPool.TryTake(archetype, out var visual))
            {
                if (_visualManifest == null)
                    return;

                var result = VisualModelBuildSystem.BuildRecipe(
                    VisualModelCatalog.GetRecipe(archetype),
                    _visualManifest,
                    parent: _visualPoolRoot.transform,
                    active: true);

                if (!result.IsComplete)
                {
                    Debug.LogWarning($"{archetype} visual recipe is missing: {string.Join(", ", result.MissingParts)}");
                    return;
                }

                visual = result.Root;
            }

            visual.transform.position = new Vector3(x, level, y);
            _entityVisuals[entity.Index] = new PooledVisualBinding
            {
                Archetype = archetype,
                Visual = visual
            };
        }

        private void ReturnVisualForEntity(Entity entity)
        {
            if (!_entityVisuals.TryGetValue(entity.Index, out var binding))
                return;

            _entityVisuals.Remove(entity.Index);
            _visualSpawnPool.Return(binding.Archetype, binding.Visual);
        }

        private struct PooledVisualBinding
        {
            public CharacterArchetype Archetype;
            public GameObject Visual;
        }

        private VisualModelComponent CreateHumanoidEnemyVisualModel(int enemyIndex)
        {
            switch ((CurrentLevel + enemyIndex) % 4)
            {
                case 0:
                    return VisualModelCatalog.CreateBandit();
                case 1:
                    return VisualModelCatalog.CreateRogue();
                case 2:
                    return VisualModelCatalog.CreateBarbarian();
                default:
                    return VisualModelCatalog.CreateKnight();
            }
        }

        public EventLog GetEventLog() => _eventLog;

        public string ExportLog() => _eventLog.ExportToJson();

        public void Dispose()
        {
            _eventLogSubscription?.Invoke();
        }

        private uint CurrentFrameNumber() => (uint)TurnCount;

        private float CurrentTimestamp() => TurnCount / 60f;

        private void PublishEntityCreated(Entity entity, string entityType, string name)
        {
            _eventBus.Publish(new EntityCreatedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                SourceEntity = entity,
                EntityType = entityType,
                Name = name
            });
        }

        private void PublishEntitySpawned(Entity entity, string entityType, string reason, int x, int y, int level)
        {
            _eventBus.Publish(new EntitySpawnedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                SourceEntity = entity,
                EntityType = entityType,
                SpawnReason = reason,
                X = x,
                Y = y,
                DungeonLevel = level
            });
        }

        private void PublishEntityDestroyed(Entity entity, string entityType, string reason)
        {
            _eventBus.Publish(new EntityDestroyedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                SourceEntity = entity,
                EntityType = entityType,
                Reason = reason
            });
        }

        private void PublishEntityMoved(Entity entity, int fromX, int fromY, int toX, int toY)
        {
            _eventBus.Publish(new EntityMovedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = CurrentFrameNumber(),
                Timestamp = CurrentTimestamp(),
                SourceEntity = entity,
                FromX = fromX,
                FromY = fromY,
                ToX = toX,
                ToY = toY
            });
        }

        private string InferEntityType(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return "Unknown";

            if (_entityManager.HasComponent<LootTableComponent>(entity))
                return "Enemy";

            if (_entityManager.HasComponent<VisionComponent>(entity))
                return "Player";

            if (_entityManager.HasComponent<DungeonLevelComponent>(entity))
                return "DungeonLevel";

            if (_entityManager.HasComponent<DungeonTile>(entity))
                return "DungeonTile";

            return "Entity";
        }

        private (int x, int y) GetEnemySpawnPosition(int enemyIndex)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var (x, y) = _generator.GetRandomSpawnPosition(DefaultLevelWidth, DefaultLevelHeight);
                if (x == DefaultPlayerX && y == DefaultPlayerY)
                    continue;

                if (!IsOccupied(x, y))
                    return (x, y);
            }

            return (10 + enemyIndex * 3, 6 + (enemyIndex % 5));
        }

        private bool IsOccupied(int x, int y)
        {
            var query = _entityManager.CreateEntityQuery(typeof(PositionComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var position = _entityManager.GetComponentData<PositionComponent>(entities[i]);
                if (position.DungeonLevel == CurrentLevel && position.X == x && position.Y == y)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Snapshot of game state for UI/debugging.</summary>
    public struct GameState
    {
        public int PlayerX;
        public int PlayerY;
        public int PlayerHealth;
        public int PlayerMaxHealth;
        public int PlayerLevel;
        public int PlayerXP;
        public int PlayerGold;
        public int CurrentLevel;
        public int TurnCount;

        public override string ToString()
        {
            return $"Level {CurrentLevel} | HP: {PlayerHealth}/{PlayerMaxHealth} | Lvl: {PlayerLevel} | XP: {PlayerXP} | Gold: {PlayerGold} | Turn: {TurnCount}";
        }
    }

    public enum PlayerTurnCommandType
    {
        Wait = 0,
        Move = 1,
        AttackNearest = 2
    }

    public struct PlayerTurnCommand
    {
        public PlayerTurnCommandType Type;
        public int DeltaX;
        public int DeltaY;

        public static PlayerTurnCommand Wait() => new() { Type = PlayerTurnCommandType.Wait };
        public static PlayerTurnCommand AttackNearest() => new() { Type = PlayerTurnCommandType.AttackNearest };
        public static PlayerTurnCommand Move(int deltaX, int deltaY) => new()
        {
            Type = PlayerTurnCommandType.Move,
            DeltaX = deltaX,
            DeltaY = deltaY
        };
    }
}
