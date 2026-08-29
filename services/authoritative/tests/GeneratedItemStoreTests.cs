using System;
using System.Collections.Generic;
using System.IO;
using Authoritative.Domain;
using Authoritative.Services;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

namespace Authoritative.Tests
{
    public class GeneratedItemStoreTests
    {
        [FactAttribute]
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
#if UNITY_5_3_OR_NEWER
                Assert.AreEqual("queue", persisted!.Metadata["source"]);
#else
                Assert.Equal("queue", persisted!.Metadata["source"]);
#endif

                var persistedFile = Path.Combine(tempDirectory, "generated-items.json");
                Assert.True(File.Exists(persistedFile));

                var reloadedStore = new GeneratedItemStore(tempDirectory);
                Assert.True(reloadedStore.TryGetItem(item.Id, out var reloaded));
                Assert.NotNull(reloaded);
#if UNITY_5_3_OR_NEWER
                Assert.AreEqual(item.Type, reloaded!.Item.Type);
                Assert.AreEqual("24", reloaded.Item.Components["damage"]);
#else
                Assert.Equal(item.Type, reloaded!.Item.Type);
                Assert.Equal("24", reloaded.Item.Components["damage"]);
#endif
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
