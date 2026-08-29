using System.Text;
using Authoritative.Domain;
using Authoritative.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Authoritative.Tests;

public class QueueConsumerTests
{
    [Fact]
    public void Process_ValidSpawnItem_SavesItemBeforeAcking()
    {
        var generator = new FakeItemGenerator();
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement();
        var metrics = new AuthoritativeMetrics();
        var processor = new QueueDeliveryProcessor(generator, store, new InMemoryProcessedMessageStore(), NullLogger.Instance, metrics: metrics);
        var delivery = Delivery(Envelope("message-1", payload: "\"payload\":{\"source\":\"queue\"}"), deliveryTag: 42);

        processor.Process(delivery, settlement);

        Assert.Single(store.SavedItems);
        Assert.Equal("queue", store.SavedItems[0].Metadata["source"]);
        Assert.Equal("message-1", store.SavedItems[0].Metadata["messageId"]);
        Assert.Equal("1", store.SavedItems[0].Metadata["contractVersion"]);
        Assert.Equal("spawn_item", store.SavedItems[0].Metadata["actionType"]);
        Assert.Equal(new[] { 42UL }, settlement.AckedDeliveryTags);
        Assert.Empty(settlement.DeadLetters);
        var prometheus = metrics.ExportPrometheus();
        Assert.Contains("authoritative_commands_received_total{action_type=\"spawn_item\"} 1", prometheus);
        Assert.Contains("authoritative_commands_succeeded_total{action_type=\"spawn_item\"} 1", prometheus);
        Assert.Contains("authoritative_ack_latency_seconds_count 1", prometheus);
    }

    [Fact]
    public void Process_InvalidEnvelope_DeadLettersWithoutSavingOrAcking()
    {
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement();
        var metrics = new AuthoritativeMetrics();
        var processor = new QueueDeliveryProcessor(new FakeItemGenerator(), store, new InMemoryProcessedMessageStore(), NullLogger.Instance, metrics: metrics);
        var delivery = Delivery("""{"contractVersion":2,"messageId":"message-2","type":"spawn_item","createdAtUtc":"2026-05-04T12:00:00Z","payload":{}}""", deliveryTag: 7);

        processor.Process(delivery, settlement);

        Assert.Empty(store.SavedItems);
        Assert.Empty(settlement.AckedDeliveryTags);
        var deadLetter = Assert.Single(settlement.DeadLetters);
        Assert.Equal(7UL, deadLetter.Delivery.DeliveryTag);
        Assert.IsType<InvalidOperationException>(deadLetter.Exception);
        Assert.Contains("authoritative_command_validation_failures_total{reason=\"schema\"} 1", metrics.ExportPrometheus());
    }

    [Fact]
    public void Process_StaleEnvelope_DeadLettersWithoutSavingOrAcking()
    {
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement();
        var processor = new QueueDeliveryProcessor(new FakeItemGenerator(), store, new InMemoryProcessedMessageStore(), NullLogger.Instance);
        var delivery = Delivery(Envelope("message-stale", expiresAtUtc: DateTime.UtcNow.AddMinutes(-1)), deliveryTag: 11);

        processor.Process(delivery, settlement);

        Assert.Empty(store.SavedItems);
        Assert.Empty(settlement.AckedDeliveryTags);
        var deadLetter = Assert.Single(settlement.DeadLetters);
        Assert.Contains("stale", deadLetter.Exception.Message);
    }

    [Fact]
    public void Process_DuplicateEnvelope_AcksWithoutMutatingAgain()
    {
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement();
        var processedMessages = new InMemoryProcessedMessageStore();
        var processor = new QueueDeliveryProcessor(new FakeItemGenerator(), store, processedMessages, NullLogger.Instance);
        var json = Envelope("message-duplicate");

        processor.Process(Delivery(json, deliveryTag: 12), settlement);
        processor.Process(Delivery(json, deliveryTag: 13), settlement);

        Assert.Single(store.SavedItems);
        Assert.Equal(new[] { 12UL, 13UL }, settlement.AckedDeliveryTags);
        Assert.Empty(settlement.DeadLetters);
    }

    [Fact]
    public void Process_PoisonJson_DeadLettersWithoutSavingOrAcking()
    {
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement();
        var processor = new QueueDeliveryProcessor(new FakeItemGenerator(), store, new InMemoryProcessedMessageStore(), NullLogger.Instance);
        var delivery = Delivery("""{"contractVersion":1,"messageId":""", deliveryTag: 14);

        processor.Process(delivery, settlement);

        Assert.Empty(store.SavedItems);
        Assert.Empty(settlement.AckedDeliveryTags);
        var deadLetter = Assert.Single(settlement.DeadLetters);
        Assert.Equal(14UL, deadLetter.Delivery.DeliveryTag);
        Assert.IsType<InvalidOperationException>(deadLetter.Exception);
    }

    [Fact]
    public void Process_AckFailureAfterSuccessfulSave_DoesNotDeadLetterProcessedMessage()
    {
        var store = new FakeGeneratedItemStore();
        var settlement = new RecordingSettlement { ThrowOnAck = true };
        var processor = new QueueDeliveryProcessor(new FakeItemGenerator(), store, new InMemoryProcessedMessageStore(), NullLogger.Instance);
        var delivery = Delivery(Envelope("message-ack-failure"), deliveryTag: 8);

        Assert.Throws<InvalidOperationException>(() => processor.Process(delivery, settlement));

        Assert.Single(store.SavedItems);
        Assert.Empty(settlement.DeadLetters);
    }


    [Fact]
    public void DeadLetterAndAck_PublishSuccess_AcksOriginalAfterConfirmedPublish()
    {
        var events = new List<string>();
        var publisher = new RecordingDeadLetterPublisher(events);
        var acknowledger = new RecordingAcknowledger(events);
        var metrics = new AuthoritativeMetrics();
        var settlement = new QueueMessageSettlement(publisher, acknowledger, NullLogger.Instance, metrics: metrics);
        var delivery = Delivery("""{"type":"unknown"}""", deliveryTag: 9);

        settlement.DeadLetterAndAck(delivery, delivery.BodyAsUtf8(), new InvalidOperationException("bad message"));

        Assert.Equal(new[] { "publish", "ack" }, events);
        Assert.Single(publisher.Published);
        Assert.Equal(new[] { 9UL }, acknowledger.AckedDeliveryTags);
        Assert.Empty(acknowledger.NackedDeliveryTags);
        Assert.Contains("authoritative_dead_letters_total{status=\"published\"} 1", metrics.ExportPrometheus());
    }

    [Fact]
    public void DeadLetterAndAck_PublishFailure_NacksOriginalForRetry()
    {
        var events = new List<string>();
        var publisher = new RecordingDeadLetterPublisher(events) { ThrowOnPublish = true };
        var acknowledger = new RecordingAcknowledger(events);
        var metrics = new AuthoritativeMetrics();
        var settlement = new QueueMessageSettlement(publisher, acknowledger, NullLogger.Instance, metrics: metrics);
        var delivery = Delivery("""{"type":"unknown"}""", deliveryTag: 10);

        settlement.DeadLetterAndAck(delivery, delivery.BodyAsUtf8(), new InvalidOperationException("bad message"));

        Assert.Equal(new[] { "publish", "nack" }, events);
        Assert.Empty(acknowledger.AckedDeliveryTags);
        var nack = Assert.Single(acknowledger.NackedDeliveryTags);
        Assert.Equal(10UL, nack.DeliveryTag);
        Assert.True(nack.Requeue);
        Assert.Contains("authoritative_dead_letters_total{status=\"failed\"} 1", metrics.ExportPrometheus());
    }

    static QueueDelivery Delivery(string json, ulong deliveryTag)
    {
        return new QueueDelivery(Encoding.UTF8.GetBytes(json), "actions", deliveryTag);
    }

    static string Envelope(string messageId, string payload = "\"payload\":{}", DateTime? expiresAtUtc = null)
    {
        var expires = expiresAtUtc.HasValue
            ? $",\"expiresAtUtc\":\"{expiresAtUtc.Value:O}\""
            : "";

        return $"{{\"contractVersion\":1,\"messageId\":\"{messageId}\",\"type\":\"spawn_item\",\"createdAtUtc\":\"{DateTime.UtcNow:O}\"{expires},{payload}}}";
    }

    sealed class FakeItemGenerator : IItemGenerator
    {
        public Item GenerateUniqueItem()
        {
            return new Item
            {
                Id = "generated-1",
                Type = "sword",
                Tier = "rare",
                Components = new Dictionary<string, string> { ["damage"] = "12" }
            };
        }
    }

    sealed class FakeGeneratedItemStore : IGeneratedItemStore
    {
        public List<(Item Item, IReadOnlyDictionary<string, string> Metadata)> SavedItems { get; } = new();

        public void SaveGeneratedItem(Item item, IReadOnlyDictionary<string, string>? metadata = null)
        {
            SavedItems.Add((item, metadata ?? new Dictionary<string, string>()));
        }

        public bool TryGetItem(string itemId, out PersistedGeneratedItem? storedItem)
        {
            storedItem = null;
            return false;
        }

        public IReadOnlyCollection<PersistedGeneratedItem> GetSnapshot()
        {
            return Array.Empty<PersistedGeneratedItem>();
        }
    }

    sealed class RecordingSettlement : IMessageSettlement
    {
        public List<ulong> AckedDeliveryTags { get; } = new();
        public List<(QueueDelivery Delivery, string Json, Exception Exception)> DeadLetters { get; } = new();
        public bool ThrowOnAck { get; set; }

        public void Ack(ulong deliveryTag)
        {
            if (ThrowOnAck)
                throw new InvalidOperationException("ack failed");

            AckedDeliveryTags.Add(deliveryTag);
        }

        public void DeadLetterAndAck(QueueDelivery delivery, string json, Exception exception)
        {
            DeadLetters.Add((delivery, json, exception));
        }
    }

    sealed class RecordingDeadLetterPublisher : IDeadLetterPublisher
    {
        readonly List<string> _events;

        public RecordingDeadLetterPublisher(List<string> events)
        {
            _events = events;
        }

        public bool ThrowOnPublish { get; set; }
        public List<QueueDelivery> Published { get; } = new();

        public void Publish(QueueDelivery delivery, Exception exception)
        {
            _events.Add("publish");
            if (ThrowOnPublish)
                throw new InvalidOperationException("publish failed");

            Published.Add(delivery);
        }
    }

    sealed class RecordingAcknowledger : IMessageAcknowledger
    {
        readonly List<string> _events;

        public RecordingAcknowledger(List<string> events)
        {
            _events = events;
        }

        public List<ulong> AckedDeliveryTags { get; } = new();
        public List<(ulong DeliveryTag, bool Requeue)> NackedDeliveryTags { get; } = new();

        public void Ack(ulong deliveryTag)
        {
            _events.Add("ack");
            AckedDeliveryTags.Add(deliveryTag);
        }

        public void Nack(ulong deliveryTag, bool requeue)
        {
            _events.Add("nack");
            NackedDeliveryTags.Add((deliveryTag, requeue));
        }
    }
}
