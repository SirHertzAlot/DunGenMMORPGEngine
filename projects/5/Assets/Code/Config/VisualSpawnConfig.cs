using System;
using System.Collections.Generic;
using DunGen.ECS.Models;

namespace DunGen.Config
{
    public sealed class VisualSpawnPoolConfig
    {
        public int DefaultInstancesPerArchetype = 1;
        public readonly List<string> EnemyKeys = new();
        public readonly Dictionary<CharacterArchetype, int> ArchetypeCounts = new();
        public readonly Dictionary<string, CharacterArchetype> EnemyArchetypes = new(StringComparer.OrdinalIgnoreCase);

        public int GetPrewarmCount(CharacterArchetype archetype)
        {
            return ArchetypeCounts.TryGetValue(archetype, out var count)
                ? count
                : DefaultInstancesPerArchetype;
        }

        public CharacterArchetype ResolveEnemyArchetype(string enemyKey, CharacterArchetype fallback)
        {
            return EnemyArchetypes.TryGetValue(enemyKey, out var archetype)
                ? archetype
                : fallback;
        }

        public string GetEnemyKeyForSpawn(int enemyIndex)
        {
            if (EnemyKeys.Count == 0)
                return $"enemy_{enemyIndex + 1}";

            int normalizedIndex = enemyIndex % EnemyKeys.Count;
            return EnemyKeys[normalizedIndex];
        }
    }

    public static class VisualSpawnConfigReader
    {
        public static VisualSpawnPoolConfig Load(ConfigLoader loader, string fileName = "enemies.yaml")
        {
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));

            var config = loader.LoadConfig(fileName);
            return FromConfig(config);
        }

        public static VisualSpawnPoolConfig FromConfig(Dictionary<string, object> config)
        {
            var result = new VisualSpawnPoolConfig();
            if (config == null)
                return result;

            if (!TryGetMap(config, "visuals", out var visuals))
                return result;

            if (TryGetMap(config, "enemies", out var enemies))
            {
                foreach (var pair in enemies)
                    result.EnemyKeys.Add(pair.Key);
            }

            if (TryGetMap(visuals, "spawnPool", out var spawnPool))
            {
                if (TryGetInt(spawnPool, "defaultInstancesPerArchetype", out var defaultCount))
                    result.DefaultInstancesPerArchetype = Math.Max(0, defaultCount);

                if (spawnPool.TryGetValue("archetypes", out var archetypeRows) && archetypeRows is List<object> archetypeList)
                {
                    for (int i = 0; i < archetypeList.Count; i++)
                    {
                        if (archetypeList[i] is not Dictionary<string, object> row)
                            continue;

                        if (!TryGetString(row, "archetype", out var archetypeName) ||
                            !TryParseArchetype(archetypeName, out var archetype))
                            continue;

                        int count = result.DefaultInstancesPerArchetype;
                        if (TryGetInt(row, "count", out var configuredCount))
                            count = Math.Max(0, configuredCount);

                        result.ArchetypeCounts[archetype] = count;
                    }
                }
            }

            if (TryGetMap(visuals, "enemyArchetypes", out var enemyArchetypes))
            {
                foreach (var pair in enemyArchetypes)
                {
                    if (pair.Value is string archetypeName && TryParseArchetype(archetypeName, out var archetype))
                        result.EnemyArchetypes[pair.Key] = archetype;
                }
            }

            return result;
        }

        public static bool TryParseArchetype(string value, out CharacterArchetype archetype)
        {
            archetype = CharacterArchetype.Bandit;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Enum.TryParse(value.Replace("_", string.Empty), true, out archetype);
        }

        private static bool TryGetMap(Dictionary<string, object> source, string key, out Dictionary<string, object> map)
        {
            map = null;
            if (!source.TryGetValue(key, out var value))
                return false;

            map = value as Dictionary<string, object>;
            return map != null;
        }

        private static bool TryGetString(Dictionary<string, object> source, string key, out string value)
        {
            value = null;
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return false;

            value = raw.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetInt(Dictionary<string, object> source, string key, out int value)
        {
            value = 0;
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return false;

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
