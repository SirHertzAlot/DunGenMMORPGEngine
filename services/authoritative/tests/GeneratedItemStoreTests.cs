using Authoritative.Domain;
using Authoritative.Services;
using Xunit;

namespace Authoritative.Tests;

public class GeneratedItemStoreTests
{
    [Fact]
    public void SaveGeneratedItem_PersistsToHotStateAndDisk()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"generated-item-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var store = new GeneratedItemStore(tempDirectory);
            var item = new Item
            {
                Id = "item-123",
                Type = "sword",
                Tier = "rare",
                Components = new Dictionary<string, string> { ["damage"] = "24" }
            };

            store.SaveGeneratedItem(item, new Dictionary<string, string> { ["source"] = "queue" });

            Assert.True(store.TryGetItem(item.Id, out var persisted));
            Assert.NotNull(persisted);
            Assert.Equal("queue", persisted!.Metadata["source"]);

            var persistedFile = Path.Combine(tempDirectory, "generated-items.json");
            Assert.True(File.Exists(persistedFile));

            var reloadedStore = new GeneratedItemStore(tempDirectory);
            Assert.True(reloadedStore.TryGetItem(item.Id, out var reloaded));
            Assert.NotNull(reloaded);
            Assert.Equal(item.Type, reloaded!.Item.Type);
            Assert.Equal("24", reloaded.Item.Components["damage"]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_QuarantinesMalformedSnapshot()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"generated-item-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var snapshotPath = Path.Combine(tempDirectory, "generated-items.json");
            File.WriteAllText(snapshotPath, "{not-valid-json");

            var store = new GeneratedItemStore(tempDirectory);

            Assert.Empty(store.GetSnapshot());
            Assert.False(File.Exists(snapshotPath));
            Assert.Single(Directory.GetFiles(tempDirectory, "generated-items.json.corrupt-*"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveGeneratedItem_ClonesInputAndReturnedValues()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"generated-item-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var store = new GeneratedItemStore(tempDirectory);
            var item = new Item
            {
                Id = "item-456",
                Type = "shield",
                Tier = "epic",
                Components = new Dictionary<string, string> { ["durability"] = "60" }
            };

            store.SaveGeneratedItem(item);
            item.Components["durability"] = "1";

            Assert.True(store.TryGetItem(item.Id, out var persisted));
            Assert.NotNull(persisted);
            Assert.Equal("60", persisted!.Item.Components["durability"]);

            persisted.Item.Components["durability"] = "2";

            Assert.True(store.TryGetItem(item.Id, out var persistedAgain));
            Assert.NotNull(persistedAgain);
            Assert.Equal("60", persistedAgain!.Item.Components["durability"]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
