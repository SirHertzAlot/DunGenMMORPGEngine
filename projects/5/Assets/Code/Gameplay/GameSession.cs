using System.Collections.Generic;
using System;
using DunGen.ECS.Combat;
using DunGen.ECS.Components;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using DunGen.ECS.Generation;
using DunGen.ECS.Systems.Combat;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Networking;
using DunGen.Simulation.RNG;
using DunGen.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Gameplay
{
    public sealed class AuthoritativeWorldBlueprint
    {
        public int Seed { get; set; }
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 24;
        public int DungeonLevel { get; set; } = 1;
        public List<AuthoritativeWorldRoomBlueprint> Rooms { get; set; } = new();
        public List<AuthoritativeWorldEnemyBlueprint> Enemies { get; set; } = new();
        public List<AuthoritativeWorldLootBlueprint> Loot { get; set; } = new();
    }

    public sealed class AuthoritativeWorldRoomBlueprint
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class AuthoritativeWorldEnemyBlueprint
    {
        public int Id { get; set; }
        public string Archetype { get; set; } = "enemy";
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; } = 1;
    }

    public sealed class AuthoritativeWorldLootBlueprint
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// Complete game session - start dungeon, handle turns, manage progression.
    /// This is the main entry point for a playable MVP session.
    /// </summary>
    public class GameSession
    {
        public enum PlayerCommandStatus
        {
            Accepted,
            Invalid,
            Duplicate,
            Stale,
            Blocked,
            Occupied,
            SessionUnavailable,
        }

        public readonly struct PlayerCommandResult
        {
            public PlayerCommandResult(PlayerCommandStatus status, string message)
            {
                Status = status;
                Message = message ?? string.Empty;
            }

            public PlayerCommandStatus Status { get; }
            public string Message { get; }
            public bool IsAccepted => Status == PlayerCommandStatus.Accepted;
        }

        public readonly struct EnemySnapshot
        {
            public EnemySnapshot(int entityId, int x, int y, int health, bool isInCombat)
            {
                EntityId = entityId;
                X = x;
                Y = y;
                Health = health;
                IsInCombat = isInCombat;
            }

            public int EntityId { get; }
            public int X { get; }
            public int Y { get; }
            public int Health { get; }
            public bool IsInCombat { get; }
        }

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
        private WorldReactionEngine _reactionEngine;
        private SimpleDungeonGenerator _generator;
        private int _nextCombatSessionId = 100;
        private readonly EntityIndexCache _entityCache;
        private readonly SpatialHashGrid _spatialGrid;
        private int _activeWorldWidth = DefaultLevelWidth;
        private int _activeWorldHeight = DefaultLevelHeight;
        private bool _usesAuthoritativeWorld;
        private bool _hasPendingPlayerMove;
        private int _pendingPlayerMoveX;
        private int _pendingPlayerMoveY;
        private bool _pendingPlayerAttack;
        private int _lastQueuedMoveTurn = -1;
        private int _lastQueuedMoveX;
        private int _lastQueuedMoveY;

        public GameSession(int seed = 12345)
        {
            _seed = seed;
            _rng = new DeterministicRNG((ulong)seed);
            _generator = new SimpleDungeonGenerator(seed);
            _eventBus = EventBus.Instance;
            _ecsWorld = World.DefaultGameObjectInjectionWorld ?? new World("DunGenGameSession");
            _entityManager = _ecsWorld.EntityManager;
            _entityCache = EntityIndexCache.Instance;
            _spatialGrid = SpatialHashGrid.Instance;
            _reactionEngine = new WorldReactionEngine(_eventBus, _entityManager);
        }

        /// <summary>Initialize a new game session.</summary>
        public void StartGame(AuthoritativeWorldBlueprint authoritativeWorld = null)
        {
            CleanupSessionEntities();

            CurrentLevel = Mathf.Max(1, authoritativeWorld?.DungeonLevel ?? 1);
            TurnCount = 0;
            IsGameOver = false;
            GameOverReason = "";
            _nextCombatSessionId = 100;
            _rng.SetSeed((uint)_seed);
            _generator = new SimpleDungeonGenerator(_seed);
            _activeWorldWidth = Mathf.Max(8, authoritativeWorld?.Width ?? DefaultLevelWidth);
            _activeWorldHeight = Mathf.Max(8, authoritativeWorld?.Height ?? DefaultLevelHeight);
            _usesAuthoritativeWorld = authoritativeWorld != null;
            _hasPendingPlayerMove = false;
            _pendingPlayerMoveX = 0;
            _pendingPlayerMoveY = 0;
            _pendingPlayerAttack = false;
            _lastQueuedMoveTurn = -1;
            _lastQueuedMoveX = 0;
            _lastQueuedMoveY = 0;

            Debug.Log("Starting new game session...");

            CreatePlayer();

            if (authoritativeWorld != null)
            {
                ApplyAuthoritativeWorld(authoritativeWorld);
                Debug.Log("Game session started from authoritative world snapshot.");
                return;
            }

            GenerateDungeonLevel(CurrentLevel, _activeWorldWidth, _activeWorldHeight);
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
                typeof(CurrencyComponent));

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

            TrackEntity(playerEntity);
            PlayerEntityId = playerEntity.Index;

            // Register in cache for O(1) lookups
            _entityCache.RegisterPlayer(playerEntity);
            _spatialGrid.UpdatePosition(playerEntity.Index, DefaultPlayerX, DefaultPlayerY, CurrentLevel);

            Debug.Log($"Player created (Entity: {PlayerEntityId})");
        }

        private void ApplyAuthoritativeWorld(AuthoritativeWorldBlueprint authoritativeWorld)
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return;

            GenerateDungeonLevel(
                CurrentLevel,
                _activeWorldWidth,
                _activeWorldHeight,
                authoritativeWorld.Rooms,
                authoritativeWorld.Enemies.Count,
                authoritativeWorld.Loot.Count);

            RepositionPlayerToAuthoritativeSpawn(playerEntity, authoritativeWorld);
            CreateEnemies(authoritativeWorld.Enemies.Count, authoritativeWorld.Enemies, authoritativeWorld.Loot);
            CreateGroundLoot(authoritativeWorld.Loot);
        }

        /// <summary>Generate a dungeon level.</summary>
        private void GenerateDungeonLevel(
            int level,
            int width = DefaultLevelWidth,
            int height = DefaultLevelHeight,
            IReadOnlyList<AuthoritativeWorldRoomBlueprint> authoritativeRooms = null,
            int enemyCount = -1,
            int lootCount = -1)
        {
            var levelEntity = _entityManager.CreateEntity(typeof(DungeonLevelComponent));

            _entityManager.SetComponentData(levelEntity, new DungeonLevelComponent
            {
                LevelNumber = level,
                Width = width,
                Height = height,
                Seed = _seed + level,
                EnemyCount = enemyCount >= 0 ? enemyCount : 5 + level,
                LootCount = lootCount >= 0 ? lootCount : 3 + level,
                IsGenerated = true
            });

            TrackEntity(levelEntity);

            var tiles = authoritativeRooms != null && authoritativeRooms.Count > 0
                ? BuildAuthoritativeTiles(level, width, height, authoritativeRooms)
                : _generator.GenerateTiles(level, width, height);

            foreach (var tile in tiles)
            {
                var tileEntity = _entityManager.CreateEntity(typeof(DungeonTile));
                _entityManager.SetComponentData(tileEntity, tile);
                TrackEntity(tileEntity);
            }

            Debug.Log($"Dungeon level {level} generated ({width}x{height}).");
        }

        private static List<DungeonTile> BuildAuthoritativeTiles(
            int level,
            int width,
            int height,
            IReadOnlyList<AuthoritativeWorldRoomBlueprint> rooms)
        {
            var walkable = new bool[width, height];
            foreach (var room in rooms)
            {
                MarkRoom(walkable, width, height, room);
            }

            for (int i = 1; i < rooms.Count; i++)
            {
                var previousCenter = GetRoomCenter(rooms[i - 1], width, height);
                var currentCenter = GetRoomCenter(rooms[i], width, height);
                CarveCorridor(walkable, width, height, previousCenter.x, previousCenter.y, currentCenter.x, previousCenter.y);
                CarveCorridor(walkable, width, height, currentCenter.x, previousCenter.y, currentCenter.x, currentCenter.y);
            }

            var tiles = new List<DungeonTile>(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    var isWalkable = !isBorder && walkable[x, y];
                    tiles.Add(new DungeonTile
                    {
                        TileType = isWalkable ? 1u : 0u,
                        LevelNumber = level,
                        GridX = x,
                        GridY = y,
                        IsWalkable = isWalkable,
                    });
                }
            }

            return tiles;
        }

        private static void MarkRoom(bool[,] walkable, int width, int height, AuthoritativeWorldRoomBlueprint room)
        {
            int startX = Mathf.Clamp(room.X, 1, Mathf.Max(1, width - 2));
            int startY = Mathf.Clamp(room.Y, 1, Mathf.Max(1, height - 2));
            int endX = Mathf.Clamp(room.X + room.Width - 1, 1, Mathf.Max(1, width - 2));
            int endY = Mathf.Clamp(room.Y + room.Height - 1, 1, Mathf.Max(1, height - 2));

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    walkable[x, y] = true;
                }
            }
        }

        private static (int x, int y) GetRoomCenter(AuthoritativeWorldRoomBlueprint room, int width, int height)
        {
            int centerX = Mathf.Clamp(room.X + Mathf.Max(0, room.Width / 2), 1, Mathf.Max(1, width - 2));
            int centerY = Mathf.Clamp(room.Y + Mathf.Max(0, room.Height / 2), 1, Mathf.Max(1, height - 2));
            return (centerX, centerY);
        }

        private static void CarveCorridor(bool[,] walkable, int width, int height, int startX, int startY, int endX, int endY)
        {
            int minX = Mathf.Clamp(Mathf.Min(startX, endX), 1, Mathf.Max(1, width - 2));
            int maxX = Mathf.Clamp(Mathf.Max(startX, endX), 1, Mathf.Max(1, width - 2));
            int minY = Mathf.Clamp(Mathf.Min(startY, endY), 1, Mathf.Max(1, height - 2));
            int maxY = Mathf.Clamp(Mathf.Max(startY, endY), 1, Mathf.Max(1, height - 2));

            for (int x = minX; x <= maxX; x++)
            {
                walkable[x, startY] = true;
            }

            for (int y = minY; y <= maxY; y++)
            {
                walkable[endX, y] = true;
            }
        }

        /// <summary>Create enemy entities.</summary>
        private void CreateEnemies(
            int count,
            IReadOnlyList<AuthoritativeWorldEnemyBlueprint> authoritativeEnemies = null,
            IReadOnlyList<AuthoritativeWorldLootBlueprint> authoritativeLoot = null)
        {
            int totalEnemies = authoritativeEnemies?.Count ?? count;
            for (int i = 0; i < totalEnemies; i++)
            {
                var authoritativeEnemy = authoritativeEnemies != null ? authoritativeEnemies[i] : null;
                int enemyLevel = Mathf.Max(1, authoritativeEnemy?.Level ?? CurrentLevel);
                var spawn = authoritativeEnemy != null
                    ? (Mathf.Clamp(authoritativeEnemy.X, 1, Mathf.Max(1, _activeWorldWidth - 2)), Mathf.Clamp(authoritativeEnemy.Y, 1, Mathf.Max(1, _activeWorldHeight - 2)))
                    : GetEnemySpawnPosition(i);

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
                    typeof(NpcPersonalityComponent),
                    typeof(NpcWorldStateComponent));

                _entityManager.SetComponentData(enemyEntity, new CombatComponent
                {
                    CurrentHealth = 35 + (enemyLevel * 15),
                    MaxHealth = 35 + (enemyLevel * 15),
                    ArmorClass = 10 + Mathf.Clamp(enemyLevel / 3, 0, 6),
                    IsInCombat = false,
                    CombatSessionId = i + 2
                });

                _entityManager.SetComponentData(enemyEntity, new CombatStatsComponent
                {
                    StrengthModifier = 1 + Mathf.Clamp(enemyLevel / 4, 0, 4),
                    DexterityModifier = Mathf.Clamp(enemyLevel / 5, 0, 3),
                    ConstitutionModifier = 1 + Mathf.Clamp(enemyLevel / 5, 0, 4),
                    IntelligenceModifier = -1,
                    ProficiencyBonus = 2 + Mathf.Clamp((enemyLevel - 1) / 4, 0, 4),
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

                _entityManager.SetComponentData(enemyEntity, new PositionComponent
                {
                    X = spawn.Item1,
                    Y = spawn.Item2,
                    DungeonLevel = CurrentLevel
                });

                _entityManager.SetComponentData(enemyEntity, new MovementComponent
                {
                    MovementSpeed = 3,
                    TilesMovedThisTurn = 0
                });

                var lootBlueprint = authoritativeLoot != null && i < authoritativeLoot.Count
                    ? authoritativeLoot[i]
                    : null;
                _entityManager.SetComponentData(enemyEntity, new LootTableComponent
                {
                    GoldDropMin = GetGoldDropMin(lootBlueprint),
                    GoldDropMax = GetGoldDropMax(lootBlueprint),
                    LootTableId = i + 1,
                    DropOnDeath = true
                });

                // Generate deterministic personality from world seed + entity index.
                string archetype = authoritativeEnemy?.Archetype ?? "enemy";
                var personality = NpcPersonalityGenerator.Generate(enemyEntity.Index, _seed, archetype);
                _entityManager.SetComponentData(enemyEntity, personality);
                _entityManager.SetComponentData(enemyEntity, new NpcWorldStateComponent());

                TrackEntity(enemyEntity);

                // Register in cache for O(1) lookups
                _entityCache.Register(enemyEntity, i + 2);
                _spatialGrid.UpdatePosition(enemyEntity.Index, spawn.Item1, spawn.Item2, CurrentLevel);
            }

            Debug.Log($"Created {totalEnemies} enemies.");
        }

        private void CreateGroundLoot(IReadOnlyList<AuthoritativeWorldLootBlueprint> lootItems)
        {
            for (int i = 0; i < lootItems.Count; i++)
            {
                var loot = lootItems[i];
                int clampedX = Mathf.Clamp(loot.X, 1, Mathf.Max(1, _activeWorldWidth - 2));
                int clampedY = Mathf.Clamp(loot.Y, 1, Mathf.Max(1, _activeWorldHeight - 2));
                var lootEntity = _entityManager.CreateEntity(typeof(ItemComponent), typeof(PositionComponent));
                _entityManager.SetComponentData(lootEntity, new ItemComponent
                {
                    ItemId = i + 1,
                    ItemName = new FixedString64Bytes($"{loot.Tier} {loot.ItemType}".Trim()),
                    Quantity = 1,
                    IsEquipped = false,
                    IsOnGround = true,
                });
                _entityManager.SetComponentData(lootEntity, new PositionComponent
                {
                    X = clampedX,
                    Y = clampedY,
                    DungeonLevel = CurrentLevel,
                });

                TrackEntity(lootEntity);
                _spatialGrid.UpdatePosition(lootEntity.Index, clampedX, clampedY, CurrentLevel);
            }
        }

        /// <summary>Execute one full game turn.</summary>
        public void ExecuteTurn()
        {
            EmitSystemExecutionEvent("turn.begin", "Turn execution started.");
            TurnCount++;

            EmitSystemExecutionEvent("movement.reset.start", "Resetting movement budgets.");
            ResetMovementForNewTurn();

            EmitSystemExecutionEvent("exploration.resolve.start", "Resolving exploration systems.");
            ResolveExplorationStep();

            EmitSystemExecutionEvent("combat.resolve.start", "Resolving combat systems.");
            ResolveCombatStep();

            EmitSystemExecutionEvent("progression.resolve.start", "Resolving rewards and progression.");
            ResolveRewardsAndProgression();

            EmitSystemExecutionEvent("reaction.end-turn.start", "Finalizing world reaction engine turn.");
            _reactionEngine.EndTurn();

            EmitSystemExecutionEvent("game-state.check.start", "Evaluating end-of-turn game state.");
            CheckGameState();

            EmitEntityAndComponentSnapshots();
            EmitSystemExecutionEvent("turn.completed", "Turn execution completed.");
        }

        public bool QueuePlayerMove(int deltaX, int deltaY)
        {
            return QueuePlayerMove(deltaX, deltaY, -1).IsAccepted;
        }

        public PlayerCommandResult QueuePlayerMove(int deltaX, int deltaY, int expectedTurn)
        {
            if (expectedTurn >= 0 && expectedTurn != TurnCount)
                return new PlayerCommandResult(PlayerCommandStatus.Stale, $"Rejected stale movement command for turn {expectedTurn}; current turn is {TurnCount}.");

            if (!TryGetPlayerEntity(out _))
                return new PlayerCommandResult(PlayerCommandStatus.SessionUnavailable, "No active player entity was found for this session.");

            if ((Mathf.Abs(deltaX) + Mathf.Abs(deltaY)) != 1)
                return new PlayerCommandResult(PlayerCommandStatus.Invalid, "Invalid movement command. Use exactly one tile per command (W/A/S/D).\n");

            if (_hasPendingPlayerMove && _pendingPlayerMoveX == deltaX && _pendingPlayerMoveY == deltaY)
                return new PlayerCommandResult(PlayerCommandStatus.Duplicate, "Duplicate movement command rejected while the same move is already queued.");

            if (_lastQueuedMoveTurn == TurnCount && _lastQueuedMoveX == deltaX && _lastQueuedMoveY == deltaY)
                return new PlayerCommandResult(PlayerCommandStatus.Duplicate, "Duplicate movement command rejected for the current turn.");

            if (!TryGetPlayerEntity(out var playerEntity)
                || !_entityManager.HasComponent<PositionComponent>(playerEntity)
                || !_entityManager.HasComponent<MovementComponent>(playerEntity))
                return new PlayerCommandResult(PlayerCommandStatus.SessionUnavailable, "Player movement state is unavailable.");

            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var movement = _entityManager.GetComponentData<MovementComponent>(playerEntity);
            if (movement.TilesMovedThisTurn >= movement.MovementSpeed)
                return new PlayerCommandResult(PlayerCommandStatus.Stale, "Movement budget exhausted for this turn.");

            int nextX = position.X + deltaX;
            int nextY = position.Y + deltaY;
            if (!_generator.IsWalkable(nextX, nextY, _activeWorldWidth, _activeWorldHeight))
                return new PlayerCommandResult(PlayerCommandStatus.Blocked, $"Blocked tile at ({nextX}, {nextY}).");

            if (IsOccupiedByLivingEnemy(nextX, nextY, position.DungeonLevel))
                return new PlayerCommandResult(PlayerCommandStatus.Occupied, $"Occupied tile at ({nextX}, {nextY}) by a living enemy.");

            _pendingPlayerMoveX = deltaX;
            _pendingPlayerMoveY = deltaY;
            _hasPendingPlayerMove = true;
            _lastQueuedMoveTurn = TurnCount;
            _lastQueuedMoveX = deltaX;
            _lastQueuedMoveY = deltaY;
            return new PlayerCommandResult(PlayerCommandStatus.Accepted, $"Queued movement command ({deltaX}, {deltaY}) for turn {TurnCount}.");
        }

        public void QueuePlayerAttack()
        {
            _pendingPlayerAttack = true;
        }

        private void EmitSystemExecutionEvent(string stage, string message)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["turn"] = TurnCount.ToString(),
                ["level"] = CurrentLevel.ToString(),
                ["gameOver"] = IsGameOver ? "1" : "0",
                ["stage"] = stage,
                ["usesAuthoritativeWorld"] = _usesAuthoritativeWorld ? "1" : "0",
            };

            BackendObservabilityBridge.TryEmitClientEvent(
                "system.execute",
                "system",
                "system:game-session",
                message,
                data,
                (uint)Time.frameCount);
        }

        private void EmitEntityAndComponentSnapshots()
        {
            const int maxSnapshotsPerTurn = 256;
            var query = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                Any = new ComponentType[]
                {
                    typeof(PositionComponent),
                    typeof(CombatComponent),
                    typeof(ExperienceComponent),
                    typeof(CurrencyComponent),
                    typeof(MovementComponent),
                    typeof(DungeonLevelComponent),
                    typeof(ItemComponent),
                    typeof(LootTableComponent),
                    typeof(NpcPersonalityComponent),
                    typeof(NpcWorldStateComponent),
                }
            });
            using var entities = query.ToEntityArray(Allocator.Temp);
            var emitted = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (emitted >= maxSnapshotsPerTurn)
                    break;

                var entity = entities[i];
                if (!_entityManager.Exists(entity))
                    continue;

                var data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["turn"] = TurnCount.ToString(),
                    ["level"] = CurrentLevel.ToString(),
                    ["entityIndex"] = entity.Index.ToString(),
                    ["entityVersion"] = entity.Version.ToString(),
                    ["componentCount"] = _entityManager.GetComponentCount(entity).ToString(),
                };

                if (_entityManager.HasComponent<PositionComponent>(entity))
                {
                    var position = _entityManager.GetComponentData<PositionComponent>(entity);
                    data["positionX"] = position.X.ToString();
                    data["positionY"] = position.Y.ToString();
                    data["positionDungeonLevel"] = position.DungeonLevel.ToString();
                    data["component.position"] = "1";
                }

                if (_entityManager.HasComponent<CombatComponent>(entity))
                {
                    var combat = _entityManager.GetComponentData<CombatComponent>(entity);
                    data["combat.currentHealth"] = combat.CurrentHealth.ToString();
                    data["combat.maxHealth"] = combat.MaxHealth.ToString();
                    data["combat.armorClass"] = combat.ArmorClass.ToString();
                    data["combat.isDead"] = combat.IsDead ? "1" : "0";
                    data["combat.isInCombat"] = combat.IsInCombat ? "1" : "0";
                    data["combat.sessionId"] = combat.CombatSessionId.ToString();
                    data["component.combat"] = "1";
                }

                if (_entityManager.HasComponent<ExperienceComponent>(entity))
                {
                    var experience = _entityManager.GetComponentData<ExperienceComponent>(entity);
                    data["progression.level"] = experience.Level.ToString();
                    data["progression.xp"] = experience.CurrentXP.ToString();
                    data["progression.xpToNext"] = experience.XPToNextLevel.ToString();
                    data["component.experience"] = "1";
                }

                if (_entityManager.HasComponent<CurrencyComponent>(entity))
                {
                    var currency = _entityManager.GetComponentData<CurrencyComponent>(entity);
                    data["economy.gold"] = currency.Gold.ToString();
                    data["component.currency"] = "1";
                }

                if (_entityManager.HasComponent<MovementComponent>(entity))
                {
                    var movement = _entityManager.GetComponentData<MovementComponent>(entity);
                    data["movement.speed"] = movement.MovementSpeed.ToString();
                    data["movement.tilesMovedThisTurn"] = movement.TilesMovedThisTurn.ToString();
                    data["component.movement"] = "1";
                }

                if (_entityManager.HasComponent<DungeonLevelComponent>(entity))
                {
                    var dungeon = _entityManager.GetComponentData<DungeonLevelComponent>(entity);
                    data["dungeon.levelNumber"] = dungeon.LevelNumber.ToString();
                    data["dungeon.width"] = dungeon.Width.ToString();
                    data["dungeon.height"] = dungeon.Height.ToString();
                    data["dungeon.enemyCount"] = dungeon.EnemyCount.ToString();
                    data["dungeon.lootCount"] = dungeon.LootCount.ToString();
                    data["component.dungeonLevel"] = "1";
                }

                if (_entityManager.HasComponent<ItemComponent>(entity))
                {
                    var item = _entityManager.GetComponentData<ItemComponent>(entity);
                    data["item.id"] = item.ItemId.ToString();
                    data["item.name"] = item.ItemName.ToString();
                    data["item.quantity"] = item.Quantity.ToString();
                    data["item.onGround"] = item.IsOnGround ? "1" : "0";
                    data["component.item"] = "1";
                }

                if (_entityManager.HasComponent<LootTableComponent>(entity))
                {
                    var loot = _entityManager.GetComponentData<LootTableComponent>(entity);
                    data["loot.tableId"] = loot.LootTableId.ToString();
                    data["loot.goldDropMin"] = loot.GoldDropMin.ToString();
                    data["loot.goldDropMax"] = loot.GoldDropMax.ToString();
                    data["loot.dropOnDeath"] = loot.DropOnDeath ? "1" : "0";
                    data["component.lootTable"] = "1";
                }

                if (_entityManager.HasComponent<NpcPersonalityComponent>(entity))
                {
                    var personality = _entityManager.GetComponentData<NpcPersonalityComponent>(entity);
                    data["npc.aggression"] = personality.Aggression.ToString();
                    data["npc.cowardice"] = personality.Cowardice.ToString();
                    data["npc.greed"] = personality.Greed.ToString();
                    data["npc.loyalty"] = personality.Loyalty.ToString();
                    data["npc.curiosity"] = personality.Curiosity.ToString();
                    data["npc.vengefulness"] = personality.Vengefulness.ToString();
                    data["npc.archetype"] = personality.ArchetypeName.ToString();
                    data["component.npcPersonality"] = "1";
                }

                if (_entityManager.HasComponent<NpcWorldStateComponent>(entity))
                {
                    var worldState = _entityManager.GetComponentData<NpcWorldStateComponent>(entity);
                    data["npc.lastDamagedByEntityIndex"] = worldState.LastDamagedByEntityIndex.ToString();
                    data["npc.fleeingTurns"] = worldState.FleeingTurns.ToString();
                    data["npc.localTension"] = worldState.LocalTension.ToString();
                    data["npc.hasReactedThisTurn"] = worldState.HasReactedThisTurn ? "1" : "0";
                    data["component.npcWorldState"] = "1";
                }

                BackendObservabilityBridge.TryEmitClientEvent(
                    "entity.state.snapshot",
                    "entity",
                    $"entity:{entity.Index}",
                    "Entity/component snapshot captured.",
                    data,
                    (uint)Time.frameCount);

                emitted++;
            }

            var summaryData = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["turn"] = TurnCount.ToString(),
                ["totalEntitiesInQuery"] = entities.Length.ToString(),
                ["emittedSnapshots"] = emitted.ToString(),
                ["maxSnapshotsPerTurn"] = maxSnapshotsPerTurn.ToString(),
            };

            BackendObservabilityBridge.TryEmitClientEvent(
                "entity.state.snapshot.summary",
                "entity",
                "system:game-session",
                "Entity/component snapshot batch completed.",
                summaryData,
                (uint)Time.frameCount);
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
                TryConsumePendingPlayerMove(playerEntity);

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

            var playerAttackRequested = _pendingPlayerAttack;
            _pendingPlayerAttack = false;

            participants.Sort(CompareByInitiativeDescending);
            var orchestrator = new CombatOrchestrator((uint)(_seed + TurnCount + playerCombat.CombatSessionId), _eventBus);

            foreach (var actor in participants)
            {
                if (!_entityManager.Exists(actor))
                    continue;

                var actorCombat = _entityManager.GetComponentData<CombatComponent>(actor);
                if (actorCombat.IsDead || !actorCombat.IsInCombat)
                    continue;

                if (actor == playerEntity && !playerAttackRequested)
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

            if (_usesAuthoritativeWorld)
            {
                IsGameOver = true;
                GameOverReason = "Authoritative world cleared.";
                Debug.Log("Authoritative world cleared. Session complete.");
                return;
            }

            CurrentLevel++;
            CleanupNonPlayerEntities(playerEntity);
            GenerateDungeonLevel(CurrentLevel, _activeWorldWidth, _activeWorldHeight);
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
                    TurnCount = TurnCount,
                    SessionSeed = _seed,
                    LivingEnemies = GetLivingEnemyCount(),
                };
            }

            var playerPos = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            var playerExp = _entityManager.GetComponentData<ExperienceComponent>(playerEntity);
            var playerCurrency = _entityManager.GetComponentData<CurrencyComponent>(playerEntity);
            var inventoryItemCount = GetInventoryItemCountEstimate();

            return new GameState
            {
                PlayerX = playerPos.X,
                PlayerY = playerPos.Y,
                PlayerHealth = playerCombat.CurrentHealth,
                PlayerMaxHealth = playerCombat.MaxHealth,
                PlayerLevel = playerExp.Level,
                PlayerXP = playerExp.CurrentXP,
                PlayerGold = playerCurrency.Gold,
                PlayerInventoryItemCount = inventoryItemCount,
                CurrentLevel = CurrentLevel,
                TurnCount = TurnCount,
                SessionSeed = _seed,
                LivingEnemies = GetLivingEnemyCount(),
                SessionState = IsGameOver ? "game_over" : "in_world"
            };
        }

        public List<EnemySnapshot> GetLivingEnemySnapshots()
        {
            var snapshots = new List<EnemySnapshot>();
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(PositionComponent), typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (combat.IsDead)
                    continue;

                var position = _entityManager.GetComponentData<PositionComponent>(entities[i]);
                snapshots.Add(new EnemySnapshot(
                    entities[i].Index,
                    position.X,
                    position.Y,
                    combat.CurrentHealth,
                    combat.IsInCombat));
            }

            return snapshots;
        }

        private int GetInventoryItemCountEstimate()
        {
            var query = _entityManager.CreateEntityQuery(typeof(ItemComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var item = _entityManager.GetComponentData<ItemComponent>(entities[i]);
                if (!item.IsOnGround)
                    count++;
            }

            return count;
        }

        public int DebugSetLivingEnemyHealth(int health)
        {
            var clampedHealth = Mathf.Max(0, health);
            int updatedCount = 0;
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (combat.IsDead)
                    continue;

                combat.CurrentHealth = Mathf.Min(combat.MaxHealth, clampedHealth);
                _entityManager.SetComponentData(entities[i], combat);
                updatedCount++;
            }

            return updatedCount;
        }

        public bool DebugSetPlayerExperience(int currentXp, int level = 1, int xpToNextLevel = 100)
        {
            if (!TryGetPlayerEntity(out var playerEntity))
                return false;

            var experience = _entityManager.GetComponentData<ExperienceComponent>(playerEntity);
            experience.CurrentXP = Mathf.Max(0, currentXp);
            experience.Level = Mathf.Max(1, level);
            experience.XPToNextLevel = Mathf.Max(1, xpToNextLevel);
            _entityManager.SetComponentData(playerEntity, experience);
            return true;
        }

        private bool TryGetPlayerEntity(out Entity playerEntity)
        {
            // O(1) lookup via EntityIndexCache instead of O(n) linear scan
            return _entityCache.TryGetPlayerEntity(out playerEntity);
        }

        private bool TryConsumePendingPlayerMove(Entity playerEntity)
        {
            if (!_hasPendingPlayerMove)
                return false;

            _hasPendingPlayerMove = false;

            if (!_entityManager.HasComponent<PositionComponent>(playerEntity)
                || !_entityManager.HasComponent<MovementComponent>(playerEntity))
                return false;

            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var movement = _entityManager.GetComponentData<MovementComponent>(playerEntity);
            if (movement.TilesMovedThisTurn >= movement.MovementSpeed)
                return false;

            int nextX = position.X + _pendingPlayerMoveX;
            int nextY = position.Y + _pendingPlayerMoveY;
            if (!_generator.IsWalkable(nextX, nextY, _activeWorldWidth, _activeWorldHeight))
                return false;

            position.X = nextX;
            position.Y = nextY;
            movement.TilesMovedThisTurn++;

            _entityManager.SetComponentData(playerEntity, position);
            _entityManager.SetComponentData(playerEntity, movement);
            _spatialGrid.UpdatePosition(playerEntity.Index, position.X, position.Y, position.DungeonLevel);
            return true;
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
            if (!_generator.IsWalkable(nextX, nextY, _activeWorldWidth, _activeWorldHeight))
                return;

            source.X = nextX;
            source.Y = nextY;
            movement.TilesMovedThisTurn++;

            _entityManager.SetComponentData(entity, source);
            _entityManager.SetComponentData(entity, movement);
            _spatialGrid.UpdatePosition(entity.Index, source.X, source.Y, source.DungeonLevel);
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

                int goldAward = Mathf.Max(loot.GoldDropMin, loot.GoldDropMax);
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

        private void RepositionPlayerForNewLevel(Entity playerEntity)
        {
            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            position.X = DefaultPlayerX;
            position.Y = DefaultPlayerY;
            position.DungeonLevel = CurrentLevel;
            _entityManager.SetComponentData(playerEntity, position);
            _spatialGrid.UpdatePosition(playerEntity.Index, position.X, position.Y, position.DungeonLevel);

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
                    _entityCache.Unregister(entity);
                    _spatialGrid.Remove(entity.Index);
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
                    _entityCache.Unregister(_sessionEntities[i]);
                    _spatialGrid.Remove(_sessionEntities[i].Index);
                    _entityManager.DestroyEntity(_sessionEntities[i]);
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

        private (int x, int y) GetEnemySpawnPosition(int enemyIndex)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var (x, y) = _generator.GetRandomSpawnPosition(_activeWorldWidth, _activeWorldHeight);
                if (x == DefaultPlayerX && y == DefaultPlayerY)
                    continue;

                if (!IsOccupied(x, y))
                    return (x, y);
            }

            return (
                Mathf.Clamp(10 + enemyIndex * 3, 1, Mathf.Max(1, _activeWorldWidth - 2)),
                Mathf.Clamp(6 + (enemyIndex % 5), 1, Mathf.Max(1, _activeWorldHeight - 2)));
        }

        private void RepositionPlayerToAuthoritativeSpawn(Entity playerEntity, AuthoritativeWorldBlueprint authoritativeWorld)
        {
            var spawn = authoritativeWorld.Rooms.Count > 0
                ? GetRoomCenter(authoritativeWorld.Rooms[0], _activeWorldWidth, _activeWorldHeight)
                : (Mathf.Clamp(_activeWorldWidth / 2, 1, Mathf.Max(1, _activeWorldWidth - 2)), Mathf.Clamp(_activeWorldHeight / 2, 1, Mathf.Max(1, _activeWorldHeight - 2)));

            var position = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            position.X = spawn.Item1;
            position.Y = spawn.Item2;
            position.DungeonLevel = CurrentLevel;
            _entityManager.SetComponentData(playerEntity, position);
            _spatialGrid.UpdatePosition(playerEntity.Index, position.X, position.Y, position.DungeonLevel);
        }

        private static int GetGoldDropMin(AuthoritativeWorldLootBlueprint loot)
        {
            if (loot == null)
                return 10;

            return loot.Tier.ToLowerInvariant() switch
            {
                "legendary" => 80,
                "epic" => 50,
                "rare" => 25,
                _ => 10,
            };
        }

        private static int GetGoldDropMax(AuthoritativeWorldLootBlueprint loot)
        {
            if (loot == null)
                return 50;

            return loot.Tier.ToLowerInvariant() switch
            {
                "legendary" => 140,
                "epic" => 90,
                "rare" => 55,
                _ => 25,
            };
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

        private bool IsOccupiedByLivingEnemy(int x, int y, int dungeonLevel)
        {
            var query = _entityManager.CreateEntityQuery(typeof(CombatComponent), typeof(PositionComponent), typeof(LootTableComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var combat = _entityManager.GetComponentData<CombatComponent>(entities[i]);
                if (combat.IsDead)
                    continue;

                var position = _entityManager.GetComponentData<PositionComponent>(entities[i]);
                if (position.DungeonLevel == dungeonLevel && position.X == x && position.Y == y)
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
        public int PlayerInventoryItemCount;
        public int CurrentLevel;
        public int TurnCount;
        public int SessionSeed;
        public int LivingEnemies;
        public string SessionState;

        public override string ToString()
        {
            return $"Seed: {SessionSeed} | Level {CurrentLevel} | HP: {PlayerHealth}/{PlayerMaxHealth} | Lvl: {PlayerLevel} | XP: {PlayerXP} | Gold: {PlayerGold} | Enemies: {LivingEnemies} | Inv: {PlayerInventoryItemCount} | Turn: {TurnCount} | State: {SessionState}";
        }
    }
}
