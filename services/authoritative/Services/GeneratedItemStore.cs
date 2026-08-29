#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Authoritative.Domain;
#if !UNITY_5_3_OR_NEWER
using Newtonsoft.Json;
#endif

namespace Authoritative.Services
{
    public interface IGeneratedItemStore
    {
        void SaveGeneratedItem(Item item, IReadOnlyDictionary<string, string>? metadata = null);
        bool TryGetItem(string itemId, out PersistedGeneratedItem? storedItem);
        IReadOnlyCollection<PersistedGeneratedItem> GetSnapshot();
    }

    public sealed class GeneratedItemStore : IGeneratedItemStore
    {
        readonly ConcurrentDictionary<string, PersistedGeneratedItem> _items = new();
        readonly object _fileLock = new();
        readonly string _filePath;

        public GeneratedItemStore()
            : this(Path.Combine(AppContext.BaseDirectory, "data"))
        {
        }

        public GeneratedItemStore(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            _filePath = Path.Combine(dataDirectory, "generated-items.json");
            LoadExistingItems();
        }

        public void SaveGeneratedItem(Item item, IReadOnlyDictionary<string, string>? metadata = null)
        {
            var stored = new PersistedGeneratedItem
            {
                Item = item,
                Metadata = metadata != null ? new Dictionary<string, string>(metadata) : new Dictionary<string, string>(),
                SavedAtUtc = DateTime.UtcNow
            };

            _items[item.Id] = stored;
            PersistSnapshot();
        }

        public bool TryGetItem(string itemId, out PersistedGeneratedItem? storedItem)
        {
            if (_items.TryGetValue(itemId, out var value))
            {
                storedItem = value;
                return true;
            }

            storedItem = null;
            return false;
        }

        public IReadOnlyCollection<PersistedGeneratedItem> GetSnapshot()
        {
            return _items.Values
                .OrderBy(entry => entry.SavedAtUtc)
                .ThenBy(entry => entry.Item.Id, StringComparer.Ordinal)
                .ToArray();
        }

        void LoadExistingItems()
        {
            if (!File.Exists(_filePath))
                return;

            var raw = File.ReadAllText(_filePath);
            var items = DeserializeItems(raw);
            if (items.Count == 0)
                return;

            foreach (var item in items)
            {
                _items[item.Item.Id] = item;
            }
        }

        void PersistSnapshot()
        {
            lock (_fileLock)
            {
                var snapshot = GetSnapshot();
                var raw = SerializeItems(snapshot);
                File.WriteAllText(_filePath, raw);
            }
        }

        static string SerializeItems(IReadOnlyCollection<PersistedGeneratedItem> snapshot)
        {
#if !UNITY_5_3_OR_NEWER
            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
#else
            var sb = new StringBuilder();
            foreach (var item in snapshot)
            {
                var fields = new[]
                {
                    Encode(item.Item.Id),
                    Encode(item.Item.Type),
                    Encode(item.Item.Tier),
                    item.SavedAtUtc.Ticks.ToString(),
                    SerializeMap(item.Metadata),
                    SerializeMap(item.Item.Components)
                };
                sb.AppendLine(string.Join("|", fields));
            }

            return sb.ToString();
#endif
        }

        static List<PersistedGeneratedItem> DeserializeItems(string raw)
        {
#if !UNITY_5_3_OR_NEWER
            return JsonConvert.DeserializeObject<List<PersistedGeneratedItem>>(raw) ?? new List<PersistedGeneratedItem>();
#else
            var result = new List<PersistedGeneratedItem>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 6)
                    continue;

                if (!long.TryParse(parts[3], out var ticks))
                    ticks = DateTime.UtcNow.Ticks;

                var item = new Item
                {
                    Id = Decode(parts[0]),
                    Type = Decode(parts[1]),
                    Tier = Decode(parts[2]),
                    Components = DeserializeMap(parts[5])
                };

                result.Add(new PersistedGeneratedItem
                {
                    Item = item,
                    Metadata = DeserializeMap(parts[4]),
                    SavedAtUtc = new DateTime(ticks, DateTimeKind.Utc)
                });
            }

            return result;
#endif
        }

#if UNITY_5_3_OR_NEWER
        static string SerializeMap(Dictionary<string, string> map)
        {
            if (map.Count == 0)
                return string.Empty;

            return string.Join(";", map.Select(kv => Encode(kv.Key) + "=" + Encode(kv.Value)));
        }

        static Dictionary<string, string> DeserializeMap(string raw)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(raw))
                return map;

            var entries = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var split = entry.Split(new[] { '=' }, 2);
                if (split.Length != 2)
                    continue;

                map[Decode(split[0])] = Decode(split[1]);
            }

            return map;
        }

        static string Encode(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
#endif
    }

    public sealed class PersistedGeneratedItem
    {
        public Item Item { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime SavedAtUtc { get; set; }
    }
}
