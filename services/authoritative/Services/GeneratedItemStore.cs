using System.Collections.Concurrent;
using System.Text;
using Authoritative.Domain;
using Newtonsoft.Json;

namespace Authoritative.Services;

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
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Id))
            throw new ArgumentException("Generated items must have a non-empty id.", nameof(item));

        var stored = new PersistedGeneratedItem
        {
            Item = CloneItem(item),
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
            storedItem = ClonePersistedItem(value);
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
            .Select(ClonePersistedItem)
            .ToArray();
    }

    void LoadExistingItems()
    {
        if (!File.Exists(_filePath))
            return;

        List<PersistedGeneratedItem>? items;
        try
        {
            var json = File.ReadAllText(_filePath);
            items = JsonConvert.DeserializeObject<List<PersistedGeneratedItem>>(json);
        }
        catch (JsonException)
        {
            QuarantineMalformedSnapshot();
            return;
        }

        if (items == null)
            return;

        foreach (var item in items)
        {
            if (!IsValidSnapshotEntry(item))
                continue;

            _items[item.Item.Id] = ClonePersistedItem(item);
        }
    }

    void PersistSnapshot()
    {
        lock (_fileLock)
        {
            var snapshot = GetSnapshot().ToArray();
            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            var tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(_filePath))
                    File.Replace(tempPath, _filePath, null);
                else
                    File.Move(tempPath, _filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    void QuarantineMalformedSnapshot()
    {
        var corruptPath = $"{_filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Move(_filePath, corruptPath);
        }
        catch (IOException)
        {
        }
    }

    static bool IsValidSnapshotEntry(PersistedGeneratedItem? item)
    {
        return item?.Item != null && !string.IsNullOrWhiteSpace(item.Item.Id);
    }

    static PersistedGeneratedItem ClonePersistedItem(PersistedGeneratedItem item)
    {
        return new PersistedGeneratedItem
        {
            Item = CloneItem(item.Item),
            Metadata = item.Metadata != null ? new Dictionary<string, string>(item.Metadata) : new Dictionary<string, string>(),
            SavedAtUtc = item.SavedAtUtc
        };
    }

    static Item CloneItem(Item item)
    {
        return new Item
        {
            Id = item.Id,
            Type = item.Type,
            Tier = item.Tier,
            Components = item.Components != null ? new Dictionary<string, string>(item.Components) : new Dictionary<string, string>()
        };
    }
}

public sealed class PersistedGeneratedItem
{
    public Item Item { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime SavedAtUtc { get; set; }
}
