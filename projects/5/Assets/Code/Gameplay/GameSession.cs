using DunGen.ECS.Combat;
using DunGen.ECS.Exploration;
using DunGen.Events;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Gameplay
{
    /// <summary>
    /// Complete game session - start dungeon, handle turns, manage progression.
    /// This is the main entry point for a playable MVP session.
    /// </summary>
    public class GameSession
    {
        public int CurrentLevel { get; set; } = 1;
        public int PlayerEntityId { get; set; } = 1;
        public int TurnCount { get; set; } = 0;
        public bool IsGameOver { get; set; } = false;
        public string GameOverReason { get; set; } = "";

        private EventBus _eventBus;
        private EntityManager _entityManager;
        private World _ecsWorld;

        public GameSession(int seed = 12345)
        {
            _eventBus = EventBus.Instance;
            _ecsWorld = World.DefaultGameObjectInjectionWorld;
            _entityManager = _ecsWorld.EntityManager;
        }

        /// <summary>Initialize a new game session.</summary>
        public void StartGame()
        {
            Debug.Log("🎮 Starting new game session...");
            
            // Create player entity
            CreatePlayer();
            
            // Generate first dungeon level
            GenerateDungeonLevel(1);
            
            // Create initial enemies
            CreateEnemies(3);
            
            Debug.Log("✅ Game session started!");
        }

        /// <summary>Create the player entity.</summary>
        private void CreatePlayer()
        {
            var playerEntity = _entityManager.CreateEntity();
            
            // Core components
            _entityManager.SetComponentData(playerEntity, new CombatComponent
            {
                CurrentHealth = 100,
                MaxHealth = 100,
                ArmorClass = 12,
                IsInCombat = false,
                IsDead = false,
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
                CurrentMana = 50,
                MaxMana = 50
            });

            // Exploration components
            _entityManager.SetComponentData(playerEntity, new PositionComponent
            {
                X = 40,
                Y = 12,
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

            PlayerEntityId = playerEntity.Index;
            Debug.Log($"✅ Player created (Entity: {PlayerEntityId})");
        }

        /// <summary>Generate a dungeon level.</summary>
        private void GenerateDungeonLevel(int level)
        {
            var levelEntity = _entityManager.CreateEntity();
            
            _entityManager.SetComponentData(levelEntity, new DungeonLevelComponent
            {
                LevelNumber = level,
                Width = 80,
                Height = 24,
                Seed = 12345 + level,
                EnemyCount = 5 + level,
                LootCount = 3 + level,
                IsGenerated = true
            });

            Debug.Log($"✅ Dungeon level {level} generated (80x24, {5 + level} enemies)");
        }

        /// <summary>Create enemy entities.</summary>
        private void CreateEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var enemyEntity = _entityManager.CreateEntity();
                
                // Combat
                _entityManager.SetComponentData(enemyEntity, new CombatComponent
                {
                    CurrentHealth = 50,
                    MaxHealth = 50,
                    ArmorClass = 11,
                    IsInCombat = false,
                    IsDead = false,
                    CombatSessionId = i + 2
                });

                _entityManager.SetComponentData(enemyEntity, new CombatStatsComponent
                {
                    StrengthModifier = 1,
                    DexterityModifier = 0,
                    ConstitutionModifier = 1,
                    IntelligenceModifier = -1
                });

                // Position
                _entityManager.SetComponentData(enemyEntity, new PositionComponent
                {
                    X = 20 + i * 10,
                    Y = 12,
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
            }

            Debug.Log($"✅ Created {count} enemies");
        }

        /// <summary>Execute one full game turn.</summary>
        public void ExecuteTurn()
        {
            TurnCount++;
            
            // Player turn
            // In real game: wait for input
            // For demo: skip
            
            // Enemy turns
            // Handled by EnemyAISystem
            
            // Check victory conditions
            CheckGameState();
        }

        /// <summary>Check if game should end.</summary>
        private void CheckGameState()
        {
            // Get player entity
            var playerEntity = _entityManager.CreateEntityQuery(typeof(VisionComponent))
                .ToEntityArray(Unity.Collections.Allocator.Temp)[0];
            
            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            
            if (playerCombat.IsDead)
            {
                IsGameOver = true;
                GameOverReason = "Player defeated!";
                Debug.Log("💀 GAME OVER: Player defeated!");
            }
        }

        /// <summary>Get current game state for display.</summary>
        public GameState GetGameState()
        {
            var query = _entityManager.CreateEntityQuery(typeof(VisionComponent));
            var playerEntity = query.ToEntityArray(Unity.Collections.Allocator.Temp)[0];
            
            var playerPos = _entityManager.GetComponentData<PositionComponent>(playerEntity);
            var playerCombat = _entityManager.GetComponentData<CombatComponent>(playerEntity);
            var playerExp = _entityManager.GetComponentData<ExperienceComponent>(playerEntity);
            var playerCurrency = _entityManager.GetComponentData<CurrencyComponent>(playerEntity);

            query.Dispose();

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
}
