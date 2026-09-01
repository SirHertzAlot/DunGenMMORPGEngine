#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Authoritative.Services
{
    /// <summary>
    /// Persistent store for terrain/dungeon grid configurations saved from the admin
    /// panel. Each grid is keyed by its id and stores the grid configuration as a raw
    /// JSON string (the panel owns the shape of that object). Additive to the existing
    /// administrative data stores and reachable only via the authenticated /admin surface.
    /// </summary>
    public interface IGridConfigStore
    {
        void Save(string gridId, string gridConfigJson);
        bool TryGet(string gridId, out PersistedGridConfig? config);
        IReadOnlyCollection<PersistedGridConfig> GetAll();
    }

    public sealed class AdminGridConfigStore : IGridConfigStore
    {
        readonly ConcurrentDictionary<string, PersistedGridConfig> _configs = new(StringComparer.Ordinal);
        readonly object _fileLock = new();
        readonly string _filePath;

        public AdminGridConfigStore()
            : this(Path.Combine(AppContext.BaseDirectory, "data", "grid-configs"))
        {
        }

        public AdminGridConfigStore(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            _filePath = Path.Combine(dataDirectory, "grid-configs.json");
            LoadExisting();
        }

        public void Save(string gridId, string gridConfigJson)
        {
            var gridIdNormalized = string.IsNullOrWhiteSpace(gridId) ? Guid.NewGuid().ToString("N") : gridId.Trim();

            var stored = new PersistedGridConfig
            {
                GridId = gridIdNormalized,
                GridConfigJson = gridConfigJson ?? string.Empty,
                SavedAtUtc = DateTime.UtcNow
            };

            _configs[gridIdNormalized] = stored;
            PersistSnapshot();
        }

        public bool TryGet(string gridId, out PersistedGridConfig? config)
        {
            if (!string.IsNullOrWhiteSpace(gridId) && _configs.TryGetValue(gridId.Trim(), out var value))
            {
                config = value;
                return true;
            }

            config = null;
            return false;
        }

        public IReadOnlyCollection<PersistedGridConfig> GetAll()
        {
            return _configs.Values
                .OrderByDescending(c => c.SavedAtUtc)
                .ToArray();
        }

        void LoadExisting()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var raw = File.ReadAllText(_filePath);
                var entries = Deserialize(raw);
                foreach (var entry in entries)
                {
                    _configs[entry.GridId] = entry;
                }
            }
            catch
            {
                // Corrupt/partial file should not prevent startup; start empty.
            }
        }

        void PersistSnapshot()
        {
            lock (_fileLock)
            {
                var raw = Serialize(GetAll().ToList());
                File.WriteAllText(_filePath, raw);
            }
        }

        static string Serialize(List<PersistedGridConfig> entries)
        {
            var lines = new List<string>();
            foreach (var e in entries)
            {
                var parts = new[]
                {
                    Encode(e.GridId),
                    Encode(e.GridConfigJson),
                    e.SavedAtUtc.Ticks.ToString()
                };
                lines.Add(string.Join("|", parts));
            }
            return string.Join("\n", lines);
        }

        static List<PersistedGridConfig> Deserialize(string raw)
        {
            var result = new List<PersistedGridConfig>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3)
                    continue;

                if (!long.TryParse(parts[2], out var ticks))
                    ticks = DateTime.UtcNow.Ticks;

                result.Add(new PersistedGridConfig
                {
                    GridId = Decode(parts[0]),
                    GridConfigJson = Decode(parts[1]),
                    SavedAtUtc = new DateTime(ticks, DateTimeKind.Utc)
                });
            }

            return result;
        }

        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));

        static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public sealed class PersistedGridConfig
    {
        public string GridId { get; set; } = "";
        public string GridConfigJson { get; set; } = "";
        public DateTime SavedAtUtc { get; set; }
    }
}
