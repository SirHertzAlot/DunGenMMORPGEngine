using DunGen.Events;
using DunGen.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DunGen.Tests
{
    public class AuthoritativeSessionStateStoreTests
    {
        private GameObject _gameObject;
        private AuthoritativeSessionStateStore _store;

        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
            _gameObject = new GameObject("AuthoritativeSessionStateStoreTests");
            _store = _gameObject.AddComponent<AuthoritativeSessionStateStore>();
            _store.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            if (_store != null)
                _store.Unsubscribe();

            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);

            EventBus.Instance.Clear();
        }

        [Test]
        public void BootstrapEvent_UpdatesSessionSnapshot()
        {
            EventBus.Instance.Publish(new AuthoritativeBootstrapReceivedEventData
            {
                EventId = 1,
                FrameNumber = 10,
                Timestamp = 3.5f,
                SessionId = "session-prod",
                HasWorld = true,
                ExecutionId = "exec-42",
                RoomCount = 9,
                EnemyCount = 17,
                LootCount = 6,
                SnapshotUrl = "http://localhost/snapshot",
                TimelineUrl = "http://localhost/timeline",
            });

            Assert.IsTrue(_store.HasBootstrap);
            Assert.IsTrue(_store.IsConnected);
            Assert.IsTrue(_store.HasWorld);
            Assert.AreEqual("session-prod", _store.SessionId);
            Assert.AreEqual("exec-42", _store.ExecutionId);
            Assert.AreEqual(9, _store.RoomCount);
            Assert.AreEqual(17, _store.EnemyCount);
            Assert.AreEqual(6, _store.LootCount);
            Assert.AreEqual("http://localhost/snapshot", _store.SnapshotUrl);
            Assert.AreEqual("http://localhost/timeline", _store.TimelineUrl);
            Assert.AreEqual(3.5f, _store.LastBootstrapTimestamp);
        }

        [Test]
        public void TimelineEvents_AreStoredInArrivalOrder_AndBounded()
        {
            for (int i = 0; i < 40; i++)
            {
                EventBus.Instance.Publish(new AuthoritativeTimelineEventReceivedEventData
                {
                    EventId = (ulong)(i + 1),
                    FrameNumber = (uint)i,
                    Timestamp = i,
                    SessionId = "session-prod",
                    RemoteEventId = $"evt-{i}",
                    RemoteEventType = "combat.damage",
                    Category = "combat",
                    RemoteFrame = (uint)i,
                    EntityId = $"entity-{i}",
                    Message = $"message-{i}",
                    TimestampUtc = $"2026-04-21T00:00:{i:00}Z",
                });
            }

            Assert.IsTrue(_store.IsConnected);
            Assert.AreEqual("session-prod", _store.SessionId);
            Assert.AreEqual(32, _store.RecentTimeline.Count);
            Assert.AreEqual("evt-8", _store.RecentTimeline[0].RemoteEventId);
            Assert.AreEqual("evt-39", _store.RecentTimeline[_store.RecentTimeline.Count - 1].RemoteEventId);
        }

        [Test]
        public void RequestFailure_MarksStoreDisconnected_AndRetainsError()
        {
            EventBus.Instance.Publish(new AuthoritativeBootstrapReceivedEventData
            {
                EventId = 1,
                FrameNumber = 1,
                Timestamp = 1,
                SessionId = "session-prod",
                HasWorld = false,
            });

            EventBus.Instance.Publish(new AuthoritativeRequestFailedEventData
            {
                EventId = 2,
                FrameNumber = 2,
                Timestamp = 2,
                SessionId = "session-prod",
                ErrorMessage = "timeout"
            });

            Assert.IsFalse(_store.IsConnected);
            Assert.AreEqual("session-prod", _store.SessionId);
            Assert.AreEqual("timeout", _store.LastError);
        }

        [Test]
        public void WorldEvent_StoresLatestWorldSnapshot()
        {
            EventBus.Instance.Publish(new AuthoritativeWorldReceivedEventData
            {
                EventId = 5,
                FrameNumber = 15,
                Timestamp = 9.5f,
                SessionId = "session-prod",
                ExecutionId = "exec-world",
                Seed = 1234,
                Width = 96,
                Height = 48,
                DungeonLevel = 3,
                Rooms = new[]
                {
                    new AuthoritativeWorldRoomData { Id = 1, X = 4, Y = 5, Width = 10, Height = 8 },
                    new AuthoritativeWorldRoomData { Id = 2, X = 22, Y = 8, Width = 12, Height = 9 },
                },
                Enemies = new[]
                {
                    new AuthoritativeWorldEnemyData { Id = 101, Archetype = "orc", X = 9, Y = 7, Level = 4 },
                },
                Loot = new[]
                {
                    new AuthoritativeWorldLootData { ItemId = "itm-1", ItemType = "weapon", Tier = "rare", X = 12, Y = 6 },
                },
                TerrainMesh = new AuthoritativeTerrainMeshData
                {
                    MeshId = "terrain-1234",
                    Width = 96,
                    Height = 48,
                    Seed = 1234,
                    Algorithm = "value-noise",
                    WaterLevel = 0.32f,
                    HeightScale = 24f,
                    MinHeight = 0f,
                    MaxHeight = 24f,
                    Vertices = new[]
                    {
                        new AuthoritativeTerrainMeshVertexData { X = 0f, Y = 0f, Z = 0f, U = 0f, V = 0f, NormalX = 0f, NormalY = 1f, NormalZ = 0f },
                        new AuthoritativeTerrainMeshVertexData { X = 1f, Y = 0.5f, Z = 0f, U = 1f, V = 0f, NormalX = 0f, NormalY = 1f, NormalZ = 0f },
                    },
                    Triangles = new[] { 0, 1, 0 },
                },
            });

            Assert.IsTrue(_store.IsConnected);
            Assert.IsTrue(_store.HasWorld);
            Assert.IsTrue(_store.HasWorldSnapshot);
            Assert.AreEqual("session-prod", _store.SessionId);
            Assert.AreEqual("exec-world", _store.ExecutionId);
            Assert.AreEqual(1234, _store.WorldSeed);
            Assert.AreEqual(96, _store.WorldWidth);
            Assert.AreEqual(48, _store.WorldHeight);
            Assert.AreEqual(3, _store.WorldDungeonLevel);
            Assert.AreEqual(2, _store.Rooms.Count);
            Assert.AreEqual(1, _store.Enemies.Count);
            Assert.AreEqual(1, _store.Loot.Count);
            Assert.IsTrue(_store.HasTerrainMesh);
            Assert.AreEqual("terrain-1234", _store.TerrainMeshId);
            Assert.AreEqual("value-noise", _store.TerrainAlgorithm);
            Assert.AreEqual(2, _store.TerrainVertices.Count);
            Assert.AreEqual(3, _store.TerrainTriangles.Count);
            Assert.AreEqual(9.5f, _store.LastWorldTimestamp);
        }
    }
}