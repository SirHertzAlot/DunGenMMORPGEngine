using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DunGen.Config
{
    /// <summary>
    /// Loads and manages all game configuration from YAML files.
    /// This is a skeleton implementation for MVP.
    /// Full YAML parsing library integration comes in Week 3.
    /// </summary>
    public class ConfigLoader
    {
        private readonly Dictionary<string, string> _configCache = new();
        private string _configPath;

        public ConfigLoader(string configPath = "config")
        {
            _configPath = configPath;
        }

        /// <summary>Load a configuration file (YAML).</summary>
        public Dictionary<string, object> LoadConfig(string fileName)
        {
            // TODO: Parse YAML file into dictionary
            // For now, return empty dictionary
            // Will integrate proper YAML library (YamlDotNet) in Week 3
            
            var filePath = Path.Combine(_configPath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Config file not found: {filePath}");
                return new Dictionary<string, object>();
            }

            if (!_configCache.TryGetValue(fileName, out var yaml))
            {
                yaml = File.ReadAllText(filePath);
                _configCache[fileName] = yaml;
            }

            // TODO: Parse YAML string to Dictionary
            return new Dictionary<string, object>();
        }

        /// <summary>Get a specific value from cached config.</summary>
        public T GetValue<T>(string configFile, string key, T defaultValue = default)
        {
            var config = LoadConfig(configFile);
            if (config.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }

        /// <summary>Clear config cache (for testing or hot-reload).</summary>
        public void ClearCache()
        {
            _configCache.Clear();
        }
    }
}
