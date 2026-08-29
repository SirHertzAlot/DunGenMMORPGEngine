using System.Collections.Generic;
using DunGen.ECS.Combat;
using DunGen.ECS.Exploration;
using DunGen.ECS.Generation;
using DunGen.ECS.Models;
using DunGen.Events;
using DunGen.Gameplay;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Tests.MVP
{
    /// <summary>
    /// Integration tests for MVP - player, enemies, combat, exploration working together.
    /// </summary>
    public class MVPIntegrationTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
        }

        #region Exploration Component Tests

        [Test]
        public void Position_SetPlayerLocation()
        {
            // Arrange
            var pos = new PositionComponent { X = 0, Y = 0 };

            // Act
            pos.X = 40;
            pos.Y = 12;
            pos.DungeonLevel = 1;

            // Assert
            Assert.AreEqual(40, pos.X);
            Assert.AreEqual(12, pos.Y);
            Assert.AreEqual(1, pos.DungeonLevel);
        }

        [Test]
        public void Vision_PlayerCanSee()
        {
            // Arrange
            var vision = new VisionComponent
            {
                VisionRange = 10,
                IsPlayerControlled = true
            };

            // Act & Assert
            Assert.AreEqual(10, vision.VisionRange);
            Assert.IsTrue(vision.IsPlayerControlled);
        }

        [Test]
        public void Movement_CanTrackMovedTiles()
        {
            // Arrange
            var movement = new MovementComponent
            {
                MovementSpeed = 5,
                TilesMovedThisTurn = 0
            };

            // Act
            movement.TilesMovedThisTurn += 3;

            // Assert
            Assert.AreEqual(3, movement.TilesMovedThisTurn);
            Assert.IsTrue(movement.TilesMovedThisTurn < movement.MovementSpeed);
        }

        [Test]
        public void Experience_LevelUpWhenXPReached()
        {
            // Arrange
            var exp = new ExperienceComponent
            {
                CurrentXP = 0,
                Level = 1,
                XPToNextLevel = 100
            };

            // Act
            exp.CurrentXP = 100;
            while (exp.CanLevelUp())
            {
                exp.LevelUp();
            }

            // Assert
            Assert.AreEqual(2, exp.Level);
            Assert.AreEqual(0, exp.CurrentXP);
            Assert.AreEqual(200, exp.XPToNextLevel);
        }

        [Test]
        public void Currency_CanTrackGold()
        {
            // Arrange
            var currency = new CurrencyComponent { Gold = 0 };

            // Act
            currency.AddGold(50);
            bool canBuy = currency.CanSpend(30);
            currency.Spend(30);

            // Assert
            Assert.AreEqual(20, currency.Gold);
            Assert.IsTrue(canBuy);
        }

        [Test]
        public void Item_CanTrackInventory()
        {
            // Arrange
            var item = new ItemComponent
            {
                ItemId = 1,
                ItemName = "Iron Sword",
                Quantity = 1,
                IsEquipped = false
            };

            // Act
            item.IsEquipped = true;

            // Assert
            Assert.IsTrue(item.IsEquipped);
            Assert.AreEqual("Iron Sword", item.ItemName.ToString());
        }

        [Test]
        public void LootTable_ConfigureLootDrop()
        {
            // Arrange
            var loot = new LootTableComponent
            {
                GoldDropMin = 10,
                GoldDropMax = 50,
                LootTableId = 1,
                DropOnDeath = true
            };

            // Act & Assert
            Assert.AreEqual(10, loot.GoldDropMin);
            Assert.AreEqual(50, loot.GoldDropMax);
            Assert.IsTrue(loot.DropOnDeath);
        }

        [Test]
        public void VisualModel_CanReferenceModularModelByName()
        {
            var visualModel = VisualModelCatalog.CreateHero();

            Assert.AreEqual(PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx, visualModel.ModelName.ToString());
            Assert.AreEqual(PolygonFantasyHeroAssetSource.HeroVariant, visualModel.VariantName.ToString());
            Assert.IsTrue(((ModelPartCategory)visualModel.RequiredParts & ModelPartCategory.MainHand) != 0);
            Assert.IsTrue(((ModelPartCategory)visualModel.HiddenParts & ModelPartCategory.Helmet) != 0);
        }

        [Test]
        public void ModularModelResolver_SelectsPartsUnderNamedRoot()
        {
            var root = new GameObject(PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx);
            var head = CreateRenderableChild(root.transform, "SK_Chr_Head_Male_00");
            var torso = CreateRenderableChild(root.transform, "SK_Chr_Torso_Male_04");
            CreateRenderableChild(root.transform, "SK_Chr_HeadCoverings_No_Hair_03");

            try
            {
                var parts = ModularModelPartResolver.SelectParts(
                    root.transform,
                    PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx,
                    ModelPartCategory.Head | ModelPartCategory.Torso | ModelPartCategory.Helmet,
                    ModelPartCategory.Helmet);

                Assert.AreEqual(2, parts.Count);
                CollectionAssert.Contains(parts, head.transform);
                CollectionAssert.Contains(parts, torso.transform);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModularModelResolver_FiltersLargerModularFbxByModelName()
        {
            var root = new GameObject("All_Modular_Characters");
            var heroRoot = new GameObject(PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx);
            heroRoot.transform.SetParent(root.transform);
            var heroHead = CreateRenderableChild(heroRoot.transform, "SK_Chr_Head_Male_00");
            CreateRenderableChild(heroRoot.transform, "SK_Wep_Sword_01");
            CreateRenderableChild(root.transform, "OtherPack_Chr_Head");
            CreateRenderableChild(root.transform, "OtherPack_Wep_Axe");

            try
            {
                var parts = ModularModelPartResolver.SelectParts(
                    root.transform,
                    PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx,
                    ModelPartCategory.Head | ModelPartCategory.MainHand);

                Assert.AreEqual(2, parts.Count);
                CollectionAssert.Contains(parts, heroHead.transform);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModularModelResolver_InfersPolygonFantasyHeroPartCategories()
        {
            Assert.IsTrue(ModularModelPartResolver.TryInferPartCategory("SK_Chr_ArmLowerLeft_Male_00", out var armCategory));
            Assert.IsTrue((armCategory & ModelPartCategory.Hands) != 0);

            Assert.IsTrue(ModularModelPartResolver.TryInferPartCategory("SK_Chr_Hips_Female_02", out var hipsCategory));
            Assert.IsTrue((hipsCategory & ModelPartCategory.Legs) != 0);

            Assert.IsTrue(ModularModelPartResolver.TryInferPartCategory("SK_Wep_Dagger_01", out var weaponCategory));
            Assert.IsTrue((weaponCategory & ModelPartCategory.MainHand) != 0);

            Assert.IsTrue(ModularModelPartResolver.TryInferPartCategory("SK_Wep_Shield_Buckler_01", out var shieldCategory));
            Assert.IsTrue((shieldCategory & ModelPartCategory.OffHand) != 0);
        }

        [Test]
        public void VisualModelCatalog_ProvidesRpgArchetypeRecipes()
        {
            var knight = VisualModelCatalog.GetRecipe(CharacterArchetype.Knight);
            var barbarian = VisualModelCatalog.GetRecipe(CharacterArchetype.Barbarian);
            var mage = VisualModelCatalog.GetRecipe(CharacterArchetype.Mage);

            Assert.AreEqual(PolygonFantasyHeroAssetSource.KnightVariant, knight.VariantName);
            Assert.AreEqual(PolygonFantasyHeroAssetSource.BarbarianVariant, barbarian.VariantName);
            Assert.AreEqual(PolygonFantasyHeroAssetSource.MageVariant, mage.VariantName);
            CollectionAssert.Contains(knight.RequiredObjectNames, "SK_Wep_Shield_01");
            CollectionAssert.Contains(barbarian.RequiredObjectNames, "SK_Wep_Axe_01");
            CollectionAssert.Contains(mage.RequiredObjectNames, "SK_Wep_Staff_01");
        }

        [Test]
        public void PolygonFantasyHeroManifest_LoadsGeneratedResource()
        {
            var manifest = PolygonFantasyHeroManifest.LoadFromResources();

            Assert.IsNotNull(manifest);
            Assert.AreEqual(1, manifest.schemaVersion);
            Assert.AreEqual(1496, manifest.assetCount);
            Assert.AreEqual(manifest.assetCount, manifest.assets.Length);
            Assert.IsTrue(manifest.ContainsAsset(PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx));
            Assert.IsTrue(manifest.ContainsAsset("SK_Chr_Torso_Male_18"));
            Assert.IsTrue(manifest.ContainsAsset("SK_Wep_Shield_Buckler_01"));
        }

        [Test]
        public void PolygonFantasyHeroManifest_ValidatesAllArchetypeRecipes()
        {
            var manifest = PolygonFantasyHeroManifest.LoadFromResources();

            Assert.IsNotNull(manifest);
            foreach (CharacterArchetype archetype in System.Enum.GetValues(typeof(CharacterArchetype)))
            {
                var recipe = VisualModelCatalog.GetRecipe(archetype);
                var missingParts = manifest.GetMissingRecipeParts(recipe);
                Assert.AreEqual(0, missingParts.Count, $"{recipe.VariantName} is missing: {string.Join(", ", missingParts)}");
            }
        }

        [Test]
        public void VisualModelBuildSystem_BuildsInactiveVisualRootFromManifest()
        {
            var manifest = PolygonFantasyHeroManifest.LoadFromResources();
            var recipe = VisualModelCatalog.GetRecipe(CharacterArchetype.Bandit);

            var result = VisualModelBuildSystem.BuildRecipe(recipe, manifest);

            try
            {
                Assert.IsTrue(result.IsComplete, string.Join(", ", result.MissingParts));
                Assert.IsNotNull(result.Root);
                Assert.IsFalse(result.Root.activeSelf);
                Assert.AreEqual(recipe.RequiredObjectNames.Length, result.MetadataPartCount);

                var poolEntry = result.Root.GetComponent<VisualSpawnPoolEntry>();
                Assert.IsNotNull(poolEntry);
                Assert.AreEqual(CharacterArchetype.Bandit, poolEntry.Archetype);
                Assert.AreEqual(recipe.RequiredObjectNames.Length, poolEntry.PartNames.Count);
                Assert.IsNotNull(result.Root.transform.Find("SK_Wep_Mace_01"));
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }

        [Test]
        public void VisualSpawnPool_PrewarmsTakesAndReturnsVisuals()
        {
            var manifest = PolygonFantasyHeroManifest.LoadFromResources();
            var poolRoot = new GameObject("Test Visual Pool");
            var pool = new VisualSpawnPool(poolRoot.transform);
            var provider = new TestModelAssetProvider();

            try
            {
                pool.Prewarm(
                    manifest,
                    new[] { CharacterArchetype.Hero, CharacterArchetype.Rogue },
                    instancesPerArchetype: 2,
                    assetProvider: provider);

                Assert.AreEqual(2, pool.Count(CharacterArchetype.Hero));
                Assert.AreEqual(2, pool.Count(CharacterArchetype.Rogue));

                Assert.IsTrue(pool.TryTake(CharacterArchetype.Hero, out var heroVisual));
                Assert.IsTrue(heroVisual.activeSelf);
                Assert.AreEqual(1, pool.Count(CharacterArchetype.Hero));

                var prefabBuiltPart = heroVisual.GetComponentInChildren<VisualModelPartInstance>();
                Assert.IsNotNull(prefabBuiltPart);
                Assert.IsTrue(prefabBuiltPart.UsedPrefabAsset);

                pool.Return(CharacterArchetype.Hero, heroVisual);
                Assert.IsFalse(heroVisual.activeSelf);
                Assert.AreEqual(2, pool.Count(CharacterArchetype.Hero));
            }
            finally
            {
                provider.DestroyCreatedPrefabs();
                Object.DestroyImmediate(poolRoot);
            }
        }

        [Test]
        public void ModularModelResolver_SelectsExactPartsFromArchetypeRecipe()
        {
            var root = new GameObject(PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx);
            var recipe = VisualModelCatalog.GetRecipe(CharacterArchetype.Knight);
            var head = CreateRenderableChild(root.transform, "SK_Chr_Head_Male_00");
            var torso = CreateRenderableChild(root.transform, "SK_Chr_Torso_Male_18");
            var shield = CreateRenderableChild(root.transform, "SK_Wep_Shield_01");
            CreateRenderableChild(root.transform, "SK_Wep_Axe_01");

            try
            {
                var parts = ModularModelPartResolver.SelectParts(root.transform, recipe);

                CollectionAssert.Contains(parts, head.transform);
                CollectionAssert.Contains(parts, torso.transform);
                CollectionAssert.Contains(parts, shield.transform);
                Assert.IsFalse(parts.Exists(part => part.name == "SK_Wep_Axe_01"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        #endregion

        #region Dungeon Generator Tests

        [Test]
        public void DungeonGenerator_CreateGenerator()
        {
            // Arrange & Act
            var generator = new SimpleDungeonGenerator(12345);

            // Assert - should not throw
            Assert.IsNotNull(generator);
        }

        [Test]
        public void DungeonGenerator_GenerateLevel()
        {
            // Arrange
            var generator = new SimpleDungeonGenerator(12345);

            // Act
            var level = generator.GenerateLevel(1, 80, 24);

            // Assert
            Assert.AreEqual(1, level.LevelNumber);
            Assert.AreEqual(80, level.Width);
            Assert.AreEqual(24, level.Height);
            Assert.IsTrue(level.IsGenerated);
            Assert.Greater(level.EnemyCount, 0);
        }

        [Test]
        public void DungeonGenerator_GetSpawnPosition()
        {
            // Arrange
            var generator = new SimpleDungeonGenerator(12345);

            // Act
            var (x, y) = generator.GetRandomSpawnPosition(80, 24);

            // Assert - should be within bounds
            Assert.Greater(x, 1);
            Assert.Less(x, 79);
            Assert.Greater(y, 1);
            Assert.Less(y, 23);
        }

        [Test]
        public void DungeonGenerator_IsWalkable()
        {
            // Arrange
            var generator = new SimpleDungeonGenerator(12345);

            // Act & Assert
            Assert.IsTrue(generator.IsWalkable(40, 12, 80, 24));  // Center should be walkable
            Assert.IsFalse(generator.IsWalkable(0, 0, 80, 24));   // Edge should not be walkable
            Assert.IsFalse(generator.IsWalkable(79, 23, 80, 24)); // Edge should not be walkable
        }

        [Test]
        public void DungeonGenerator_GetDistance()
        {
            // Arrange
            var generator = new SimpleDungeonGenerator(12345);

            // Act
            int dist = generator.GetDistance(0, 0, 3, 4);

            // Assert
            Assert.AreEqual(7, dist);  // Manhattan distance: |3-0| + |4-0| = 7
        }

        [Test]
        public void DungeonGenerator_GetMoveTowards()
        {
            // Arrange
            var generator = new SimpleDungeonGenerator(12345);

            // Act
            var (dx, dy) = generator.GetMoveTowards(0, 0, 3, 4);

            // Assert
            Assert.AreEqual(1, dx);   // Moving towards positive X
            Assert.AreEqual(1, dy);   // Moving towards positive Y
        }

        [Test]
        public void DungeonGenerator_GenerateTiles_CreatesWalkableInterior()
        {
            var generator = new SimpleDungeonGenerator(12345);

            var tiles = generator.GenerateTiles(1, 10, 6);

            Assert.AreEqual(60, tiles.Count);
            Assert.IsFalse(tiles[0].IsWalkable);
            Assert.IsTrue(tiles.Exists(tile => tile.GridX == 5 && tile.GridY == 3 && tile.IsWalkable));
        }

        #endregion

        #region Game Session Tests

        [Test]
        public void GameSession_CreateSession()
        {
            // Arrange & Act
            var session = new GameSession(12345);

            // Assert
            Assert.AreEqual(1, session.CurrentLevel);
            Assert.AreEqual(0, session.TurnCount);
            Assert.IsFalse(session.IsGameOver);
        }

        [Test]
        public void GameSession_GetGameState()
        {
            // Arrange
            var session = new GameSession(12345);
            session.StartGame();

            // Act
            var state = session.GetGameState();

            // Assert
            Assert.Greater(state.PlayerHealth, 0);
            Assert.AreEqual(1, state.PlayerLevel);
            Assert.AreEqual(1, state.CurrentLevel);
        }

        [Test]
        public void GameSession_ExecuteTurn_IncrementsTurnCount()
        {
            // Arrange
            var session = new GameSession(12345);
            session.StartGame();
            Assert.AreEqual(0, session.TurnCount);

            // Act
            session.ExecuteTurn();

            // Assert
            Assert.AreEqual(1, session.TurnCount);
        }

        [Test]
        public void GameSession_MultipleStartsResetState()
        {
            // Arrange
            var session = new GameSession(12345);
            session.StartGame();
            session.ExecuteTurn();
            Assert.AreEqual(1, session.TurnCount);

            // Act
            session.StartGame();

            // Assert
            Assert.AreEqual(0, session.TurnCount);  // Reset
        }

        #endregion

        #region Integration Scenarios

        [Test]
        public void CompleteGameLoop_CreatePlayerMoveKillEnemy()
        {
            // Arrange
            var session = new GameSession(12345);
            session.StartGame();
            var initialState = session.GetGameState();

            // Act - Execute multiple turns
            for (int i = 0; i < 10; i++)
            {
                session.ExecuteTurn();
            }

            var afterState = session.GetGameState();

            // Assert
            Assert.AreEqual(10, session.TurnCount);
            Assert.AreEqual(initialState.CurrentLevel, afterState.CurrentLevel);
            Assert.IsFalse(session.IsGameOver);  // Game should still be running
        }

        [Test]
        public void PlayerProgression_GainXPAndLevel()
        {
            // Arrange
            var exp = new ExperienceComponent
            {
                CurrentXP = 50,
                Level = 1,
                XPToNextLevel = 100
            };

            // Act
            exp.CurrentXP += 50;  // Gain 50 XP
            
            while (exp.CanLevelUp())
            {
                exp.LevelUp();
            }

            // Assert
            Assert.AreEqual(2, exp.Level);
            Assert.AreEqual(0, exp.CurrentXP);
        }

        [Test]
        public void CombatEncounter_PlayerVsEnemy()
        {
            // Arrange
            var player = new CombatComponent { CurrentHealth = 100, MaxHealth = 100 };
            var enemy = new CombatComponent { CurrentHealth = 50, MaxHealth = 50 };

            // Act - Simulate damage
            int damageDealt = 20;
            enemy.CurrentHealth -= damageDealt;

            // Assert
            Assert.AreEqual(30, enemy.CurrentHealth);
            Assert.IsTrue(enemy.CurrentHealth > 0);  // Enemy still alive
        }

        [Test]
        public void Exploration_PlayerCanMoveAroundLevel()
        {
            // Arrange
            var playerPos = new PositionComponent { X = 40, Y = 12, DungeonLevel = 1 };
            var generator = new SimpleDungeonGenerator(12345);

            // Act - Move player
            var (dx, dy) = generator.GetMoveTowards(playerPos.X, playerPos.Y, 50, 15);
            playerPos.X += dx;
            playerPos.Y += dy;

            // Assert
            Assert.AreEqual(41, playerPos.X);
            Assert.AreEqual(13, playerPos.Y);
            Assert.IsTrue(generator.IsWalkable(playerPos.X, playerPos.Y, 80, 24));
        }

        #endregion

        #region Determinism Tests

        [Test]
        public void Determinism_SameSeedProducesSameEncounters()
        {
            // Arrange
            var gen1 = new SimpleDungeonGenerator(12345);
            var gen2 = new SimpleDungeonGenerator(12345);

            // Act
            var level1 = gen1.GenerateLevel(1);
            var level2 = gen2.GenerateLevel(1);

            // Assert
            Assert.AreEqual(level1.EnemyCount, level2.EnemyCount);
            Assert.AreEqual(level1.LootCount, level2.LootCount);
        }

        [Test]
        public void Determinism_DifferentSeedsProduceDifferentEncounters()
        {
            // Arrange
            var gen1 = new SimpleDungeonGenerator(12345);
            var gen2 = new SimpleDungeonGenerator(54321);

            // Act
            var level1 = gen1.GenerateLevel(1);
            var level2 = gen2.GenerateLevel(1);

            // Assert - At least one should differ
            Assert.IsFalse(level1.EnemyCount == level2.EnemyCount && level1.LootCount == level2.LootCount);
        }

        #endregion

        private static GameObject CreateRenderableChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.AddComponent<MeshFilter>();
            return child;
        }

        private sealed class TestModelAssetProvider : IModelAssetProvider
        {
            private readonly List<GameObject> _createdPrefabs = new();

            public GameObject LoadPrefab(ModelAssetEntry asset)
            {
                var prefab = new GameObject(asset.name + "_Prefab");
                prefab.AddComponent<MeshFilter>();
                _createdPrefabs.Add(prefab);
                return prefab;
            }

            public void DestroyCreatedPrefabs()
            {
                for (int i = 0; i < _createdPrefabs.Count; i++)
                    Object.DestroyImmediate(_createdPrefabs[i]);
            }
        }
    }
}
