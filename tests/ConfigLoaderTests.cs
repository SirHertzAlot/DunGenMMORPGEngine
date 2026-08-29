using System;
using System.Collections.Generic;
using System.IO;
using DunGen.Config;
using DunGen.ECS.Models;
using DunGen.Gameplay;
using NUnit.Framework;

namespace DunGen.Tests
{
    public class ConfigLoaderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"dungen-config-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void LoadConfig_ParsesNestedMappingsAndLists()
        {
            var yaml = @"
root:
  number: 42
  ratio: 0.5
  enabled: true
  title: ""Dungeon""
  tags:
    - ""roguelike""
    - ""co-op""
  options: [""fast"", ""safe""]
";

            File.WriteAllText(Path.Combine(_tempDir, "sample.yaml"), yaml);
            var loader = new ConfigLoader(_tempDir);

            Assert.AreEqual(42, loader.GetValue<int>("sample.yaml", "root.number"));
            Assert.AreEqual(0.5, loader.GetValue<double>("sample.yaml", "root.ratio"));
            Assert.AreEqual(true, loader.GetValue<bool>("sample.yaml", "root.enabled"));
            Assert.AreEqual("Dungeon", loader.GetValue<string>("sample.yaml", "root.title"));
            Assert.AreEqual("roguelike", loader.GetValue<string>("sample.yaml", "root.tags.0"));
            Assert.AreEqual("safe", loader.GetValue<string>("sample.yaml", "root.options.1"));
        }

        [Test]
        public void GetValue_ReturnsDefaultForMissingKeyOrTypeMismatch()
        {
            File.WriteAllText(Path.Combine(_tempDir, "sample.yaml"), "root:\n  value: 10\n");
            var loader = new ConfigLoader(_tempDir);

            Assert.AreEqual(-1, loader.GetValue("sample.yaml", "root.missing", -1));
            Assert.AreEqual("fallback", loader.GetValue("sample.yaml", "root.value", "fallback"));
        }

        [Test]
        public void ClearCache_RefreshesYamlContent()
        {
            var filePath = Path.Combine(_tempDir, "sample.yaml");
            File.WriteAllText(filePath, "root:\n  value: 10\n");

            var loader = new ConfigLoader(_tempDir);
            Assert.AreEqual(10, loader.GetValue<int>("sample.yaml", "root.value"));

            File.WriteAllText(filePath, "root:\n  value: 99\n");

            // Cached raw YAML keeps old value until cache is cleared.
            Assert.AreEqual(10, loader.GetValue<int>("sample.yaml", "root.value"));

            loader.ClearCache();
            Assert.AreEqual(99, loader.GetValue<int>("sample.yaml", "root.value"));
        }

        [Test]
        public void LoadConfig_ParsesListOfMaps()
        {
            var yaml = @"
enemyDrops:
  goblin:
    drops:
      - rarity: ""common""
        weight: 3
      - rarity: ""rare""
        weight: 1
";

            File.WriteAllText(Path.Combine(_tempDir, "sample.yaml"), yaml);
            var loader = new ConfigLoader(_tempDir);

            var firstDrop = loader.GetValue<Dictionary<string, object>>("sample.yaml", "enemyDrops.goblin.drops.0");
            var secondDropRarity = loader.GetValue<string>("sample.yaml", "enemyDrops.goblin.drops.1.rarity");

            Assert.NotNull(firstDrop);
            Assert.AreEqual(3, Convert.ToInt32(firstDrop["weight"]));
            Assert.AreEqual("rare", secondDropRarity);
        }

        [Test]
        public void VisualSpawnConfig_LoadsEnemyKeysAndArchetypeMappingsInYamlOrder()
        {
            var yaml = @"
enemies:
  goblin:
    name: ""Goblin""
  orc:
    name: ""Orc""
  skeleton:
    name: ""Skeleton""
visuals:
  enemyArchetypes:
    goblin: ""bandit""
    orc: ""barbarian""
    skeleton: ""knight""
";

            File.WriteAllText(Path.Combine(_tempDir, "enemies.yaml"), yaml);
            var config = VisualSpawnConfigReader.Load(new ConfigLoader(_tempDir));

            CollectionAssert.AreEqual(new[] { "goblin", "orc", "skeleton" }, config.EnemyKeys);
            Assert.AreEqual("goblin", config.GetEnemyKeyForSpawn(0));
            Assert.AreEqual("orc", config.GetEnemyKeyForSpawn(1));
            Assert.AreEqual("skeleton", config.GetEnemyKeyForSpawn(2));
            Assert.AreEqual("goblin", config.GetEnemyKeyForSpawn(3));
            Assert.AreEqual(CharacterArchetype.Bandit, config.ResolveEnemyArchetype("goblin", CharacterArchetype.Rogue));
        }

        [Test]
        public void GameSession_ResolvesEnemyVisualArchetypeFromConfiguredEnemyKey()
        {
            var config = new VisualSpawnPoolConfig();
            config.EnemyKeys.Add("goblin");
            config.EnemyArchetypes["goblin"] = CharacterArchetype.Bandit;

            using var session = new GameSession(seed: 12345, visualSpawnConfig: config, visualManifest: new ModelAssetManifest());
            session.CurrentLevel = 1;

            Assert.AreEqual(CharacterArchetype.Bandit, session.ResolveEnemyVisualArchetype(config.GetEnemyKeyForSpawn(0), 0));
        }
    }
}
