using Authoritative.Diagnostics;
using Xunit;

namespace Authoritative.Tests;

public class DiagnosticLogStoreTests
{
    [Fact]
    public void Record_PersistsSourceMetadataAndCanQueryByCorrelation()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var store = new DiagnosticLogStore(tempDirectory);

            var entry = RecordFromHelper(store, "corr-123");

            Assert.Equal("corr-123", entry.CorrelationId);
            Assert.EndsWith("DiagnosticLogStoreTests.cs", entry.SourceFile);
            Assert.Equal(nameof(RecordFromHelper), entry.SourceMember);
            Assert.True(entry.SourceLine > 0);

            var result = store.Query(new DiagnosticLogQuery
            {
                CorrelationId = "corr-123",
                SourceMember = nameof(RecordFromHelper)
            });

            var queried = Assert.Single(result.Entries);
            Assert.Equal(entry.Id, queried.Id);

            var reloadedStore = new DiagnosticLogStore(tempDirectory);
            Assert.NotNull(reloadedStore.Get(entry.Id));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Query_FiltersByTagsTextAndCapsTake()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var store = new DiagnosticLogStore(tempDirectory);
            store.Record(new DiagnosticLogWriteRequest
            {
                Level = "Debug",
                Category = "ecs.spawn",
                EventName = "entity.spawned",
                Message = "Spawned goblin",
                Tags = new Dictionary<string, string> { ["phase"] = "spawn" },
                Properties = new Dictionary<string, string> { ["entityType"] = "goblin" }
            });
            store.Record(new DiagnosticLogWriteRequest
            {
                Level = "Information",
                Category = "ecs.move",
                EventName = "entity.moved",
                Message = "Moved hero",
                Tags = new Dictionary<string, string> { ["phase"] = "movement" }
            });

            var result = store.Query(new DiagnosticLogQuery
            {
                TextContains = "goblin",
                Tags = new Dictionary<string, string> { ["phase"] = "spawn" },
                Take = 5000
            });

            Assert.Equal(1, result.Total);
            Assert.Equal(1000, result.Take);
            Assert.Equal("entity.spawned", Assert.Single(result.Entries).EventName);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void UpdateAndDelete_RewritesDurableLog()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var store = new DiagnosticLogStore(tempDirectory);
            var entry = store.Record(new DiagnosticLogWriteRequest
            {
                Level = "Warning",
                Category = "queue.actions",
                EventName = "action.processing_failed",
                Message = "Initial message"
            });

            Assert.True(store.TryUpdate(entry.Id, new DiagnosticLogUpdateRequest
            {
                Message = "Annotated message",
                RetentionClass = "incident",
                Tags = new Dictionary<string, string> { ["reviewed"] = "true" }
            }, out var updated));

            Assert.NotNull(updated);
            Assert.Equal("Annotated message", updated!.Message);
            Assert.Equal("incident", updated.RetentionClass);
            Assert.Equal("true", updated.Tags["reviewed"]);

            var reloadedStore = new DiagnosticLogStore(tempDirectory);
            Assert.Equal("Annotated message", reloadedStore.Get(entry.Id)?.Message);

            Assert.True(reloadedStore.TryDelete(entry.Id));
            Assert.Null(new DiagnosticLogStore(tempDirectory).Get(entry.Id));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    static DiagnosticLogEntry RecordFromHelper(DiagnosticLogStore store, string correlationId)
    {
        return store.Record(new DiagnosticLogWriteRequest
        {
            Level = "Information",
            Category = "test.category",
            EventName = "test.recorded",
            Message = "recorded for test",
            CorrelationId = correlationId
        });
    }

    static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"diagnostic-log-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
