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
        /// Parse a YAML string into a nested dictionary.
        /// Supports scalar values, nested mappings, and sequences.
        /// </summary>
        private static Dictionary<string, object> ParseYaml(string yaml)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(yaml))
                return result;

            var lines = yaml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Stack entries: (indentOfKey, container) where container is
            // Dictionary<string,object> for mappings or List<object> for sequences.
            var stack = new Stack<(int indent, object container)>();
            stack.Push((-1, (object)result));

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith('#'))
                    continue;

                int indent = raw.Length - raw.TrimStart().Length;
                var trimmed = raw.TrimStart();

                // Pop frames that belong at the same or deeper indent level.
                while (stack.Count > 1 && stack.Peek().indent >= indent)
                    stack.Pop();

                var parentContainer = stack.Peek().container;

                if (trimmed.StartsWith("- "))
                {
                    // Sequence item
                    string itemValue = trimmed.Substring(2).Trim();
                    if (parentContainer is List<object> list)
                        list.Add(ParseYamlValue(itemValue));
                }
                else
                {
                    int colonIdx = trimmed.IndexOf(':');
                    if (colonIdx < 0)
                        continue;

                    string key = trimmed.Substring(0, colonIdx).Trim();
                    string valueStr = colonIdx + 1 < trimmed.Length
                        ? trimmed.Substring(colonIdx + 1).Trim()
                        : "";

                    if (!(parentContainer is Dictionary<string, object> dict))
                        continue;

                    if (string.IsNullOrEmpty(valueStr))
                    {
                        // Look ahead to decide if children are a sequence or mapping.
                        bool nextIsSequence = false;
                        for (int j = i + 1; j < lines.Length; j++)
                        {
                            var nextRaw = lines[j].TrimEnd();
                            if (string.IsNullOrWhiteSpace(nextRaw) || nextRaw.TrimStart().StartsWith('#'))
                                continue;
                            int nextIndent = nextRaw.Length - nextRaw.TrimStart().Length;
                            if (nextIndent <= indent)
                                break;
                            nextIsSequence = nextRaw.TrimStart().StartsWith("- ");
                            break;
                        }

                        if (nextIsSequence)
                        {
                            var list = new List<object>();
                            dict[key] = list;
                            stack.Push((indent, (object)list));
                        }
                        else
                        {
                            var nested = new Dictionary<string, object>();
                            dict[key] = nested;
                            stack.Push((indent, (object)nested));
                        }
                    }
                    else
                    {
                        dict[key] = ParseYamlValue(valueStr);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Convert a YAML scalar string to the most appropriate CLR type.
        /// </summary>
        private static object ParseYamlValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "null" || value == "~")
                return null;
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
                return value.Substring(1, value.Length - 2);
            if (value == "true") return true;
            if (value == "false") return false;
            if (int.TryParse(value, out int intVal)) return intVal;
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
                return floatVal;
            return value;
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
