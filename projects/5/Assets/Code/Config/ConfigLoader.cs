using System;
using System.Collections.Generic;
using System.Globalization;
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

            return ParseYaml(yaml);
        }

        /// <summary>
        /// Parse a flat YAML string (key: value pairs) into a dictionary.
        /// Handles strings, booleans, integers, and floats.
        /// Nested objects and YAML lists are not supported.
        /// </summary>
        private Dictionary<string, object> ParseYaml(string yaml)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(yaml))
                return result;

            foreach (var line in yaml.Split('\n'))
            {
                var trimmed = line.Trim();

                // Skip blank lines and comments
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                var key = trimmed.Substring(0, colonIndex).Trim();
                var valueStr = trimmed.Substring(colonIndex + 1).Trim();

                // Strip inline comments — a '#' preceded by any whitespace starts a comment
                for (int i = 1; i < valueStr.Length; i++)
                {
                    if (valueStr[i] == '#' && char.IsWhiteSpace(valueStr[i - 1]))
                    {
                        valueStr = valueStr.Substring(0, i).TrimEnd();
                        break;
                    }
                }

                result[key] = ParseYamlValue(valueStr);
            }

            return result;
        }

        /// <summary>
        /// Convert a raw YAML value string into a typed object.
        /// Supports booleans, integers, floats, and quoted/unquoted strings.
        /// </summary>
        private static object ParseYamlValue(string valueStr)
        {
            if (string.IsNullOrEmpty(valueStr))
                return string.Empty;

            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (int.TryParse(valueStr, out int intVal))
                return intVal;

            if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                return floatVal;

            // Strip surrounding quotes from string values
            if (valueStr.Length >= 2 &&
                ((valueStr[0] == '"' && valueStr[valueStr.Length - 1] == '"') ||
                 (valueStr[0] == '\'' && valueStr[valueStr.Length - 1] == '\'')))
                return valueStr.Substring(1, valueStr.Length - 2);

            return valueStr;
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
