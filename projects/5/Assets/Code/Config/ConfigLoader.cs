using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DunGen.Config
{
    /// <summary>
    /// Loads and manages all game configuration from YAML files.
    /// This parser intentionally supports the YAML subset currently used by
    /// this repository's config files (mappings, inline lists, and block lists).
    /// </summary>
    public class ConfigLoader
    {
        private readonly Dictionary<string, string> _configCache = new();
        private readonly string _configPath;

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
        /// Get a specific value from cached config.
        /// Supports dotted paths for nested values, e.g. "generation.gridSize".
        /// Lists can be indexed, e.g. "lootTables.common.items.0".
        /// </summary>
        public T GetValue<T>(string configFile, string key, T defaultValue = default)
        {
            var config = LoadConfig(configFile);
            if (!TryGetNestedValue(config, key, out var value))
                return defaultValue;

            if (value is T typed)
                return typed;

            // Only perform numeric widening/narrowing conversions.
            // A string key that holds a non-string value (or vice-versa) is a
            // type mismatch and should return the caller-supplied default.
            if (typeof(T) != typeof(string) && value is not string && value is IConvertible)
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                }
                catch
                {
                    // Use provided default value.
                }
            }

            return defaultValue;
        }

        /// <summary>Clear config cache (for testing or hot-reload).</summary>
        public void ClearCache()
        {
            _configCache.Clear();
        }

        private static Dictionary<string, object> ParseYaml(string yaml)
        {
            var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<YamlContainer>();
            stack.Push(new YamlContainer(root, -1));

            var lines = yaml.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = StripComments(lines[i]);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int indent = CountIndent(line);
                string trimmed = line.Trim();

                while (stack.Count > 1 && indent <= stack.Peek().Indent)
                {
                    stack.Pop();
                }

                var current = stack.Peek();
                if (trimmed.StartsWith("- "))
                {
                    if (current.Value is not List<object> list)
                        continue;

                    string itemText = trimmed.Substring(2).Trim();
                    if (string.IsNullOrEmpty(itemText))
                    {
                        var nestedMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        list.Add(nestedMap);
                        stack.Push(new YamlContainer(nestedMap, indent));
                    }
                    else if (TryParseInlineKeyValue(itemText, out var itemKey, out var itemValueText))
                    {
                        var nestedMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            [itemKey] = ParseScalarOrInlineList(itemValueText)
                        };
                        list.Add(nestedMap);
                        stack.Push(new YamlContainer(nestedMap, indent));
                    }
                    else
                    {
                        list.Add(ParseScalarOrInlineList(itemText));
                    }

                    continue;
                }

                if (!TryParseInlineKeyValue(trimmed, out var key, out var valueText))
                    continue;

                if (current.Value is not Dictionary<string, object> map)
                    continue;

                if (string.IsNullOrEmpty(valueText))
                {
                    bool nextIsList = IsNextMeaningfulLineListItem(lines, i, indent);
                    object child = nextIsList
                        ? new List<object>()
                        : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    map[key] = child;
                    stack.Push(new YamlContainer(child, indent));
                }
                else
                {
                    map[key] = ParseScalarOrInlineList(valueText);
                }
            }

            return root;
        }

        private static bool TryGetNestedValue(Dictionary<string, object> root, string keyPath, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(keyPath))
                return false;

            object current = root;
            foreach (var segment in keyPath.Split('.'))
            {
                if (current is Dictionary<string, object> map && map.TryGetValue(segment, out var next))
                {
                    current = next;
                    continue;
                }

                if (current is List<object> list && int.TryParse(segment, out var index) && index >= 0 && index < list.Count)
                {
                    current = list[index];
                    continue;
                }

                return false;
            }

            value = current;
            return true;
        }

        private static string StripComments(string line)
        {
            bool inDoubleQuotes = false;
            bool inSingleQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' && !inSingleQuotes)
                    inDoubleQuotes = !inDoubleQuotes;
                else if (line[i] == '\'' && !inDoubleQuotes)
                    inSingleQuotes = !inSingleQuotes;
                else if (line[i] == '#' && !inDoubleQuotes && !inSingleQuotes)
                    return line.Substring(0, i);
            }

            return line;
        }

        private static int CountIndent(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
                count++;
            return count;
        }

        private static bool TryParseInlineKeyValue(string trimmed, out string key, out string value)
        {
            key = null;
            value = null;

            int separator = trimmed.IndexOf(':');
            if (separator <= 0)
                return false;

            key = trimmed.Substring(0, separator).Trim();
            value = trimmed.Substring(separator + 1).Trim();
            return key.Length > 0;
        }

        private static object ParseScalarOrInlineList(string valueText)
        {
            if (valueText.StartsWith("[") && valueText.EndsWith("]"))
            {
                var inner = valueText.Substring(1, valueText.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                    return new List<object>();

                return inner.Split(',')
                    .Select(item => ParseScalar(item.Trim()))
                    .ToList();
            }

            return ParseScalar(valueText);
        }

        private static object ParseScalar(string text)
        {
            string unquoted = Unquote(text);

            if (string.Equals(unquoted, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(unquoted, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (int.TryParse(unquoted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                return intValue;
            if (long.TryParse(unquoted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                return longValue;
            if (double.TryParse(unquoted, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            return unquoted;
        }

        private static string Unquote(string text)
        {
            if (text.Length >= 2 &&
                ((text.StartsWith("\"") && text.EndsWith("\"")) ||
                 (text.StartsWith("'") && text.EndsWith("'"))))
            {
                return text.Substring(1, text.Length - 2);
            }

            return text;
        }

        private static bool IsNextMeaningfulLineListItem(string[] lines, int currentIndex, int currentIndent)
        {
            for (int i = currentIndex + 1; i < lines.Length; i++)
            {
                var candidate = StripComments(lines[i]);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                int indent = CountIndent(candidate);
                if (indent <= currentIndent)
                    return false;

                return candidate.TrimStart().StartsWith("- ");
            }

            return false;
        }

        private readonly struct YamlContainer
        {
            public YamlContainer(object value, int indent)
            {
                Value = value;
                Indent = indent;
            }

            public object Value { get; }
            public int Indent { get; }
        }
    }
}
