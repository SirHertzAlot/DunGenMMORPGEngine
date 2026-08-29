using DunGen.Events;
using DunGen.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DunGen.Tests
{
    public class AuthoritativeWorldSceneRendererTests
    {
        private GameObject _gameObject;
        private AuthoritativeSessionStateStore _store;
        private AuthoritativeWorldSceneRenderer _renderer;

        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
            _gameObject = new GameObject("AuthoritativeWorldSceneRendererTests");
            _store = _gameObject.AddComponent<AuthoritativeSessionStateStore>();
            _store.Subscribe();
            _renderer = _gameObject.AddComponent<AuthoritativeWorldSceneRenderer>();
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
        public void RefreshNow_RendersTerrainMeshAndMarkers()
        {
            EventBus.Instance.Publish(new AuthoritativeWorldReceivedEventData
            {
                EventId = 1,
                FrameNumber = 10,
                Timestamp = 1.5f,
                SessionId = "session-prod",
                ExecutionId = "exec-world",
                Seed = 1234,
                Width = 2,
                Height = 2,
                DungeonLevel = 1,
                Rooms = new[]
                {
                    new AuthoritativeWorldRoomData { Id = 1, X = 0, Y = 0, Width = 1, Height = 1 },
                },
                Enemies = new[]
                {
                    new AuthoritativeWorldEnemyData { Id = 2, Archetype = "orc", X = 1, Y = 1, Level = 2 },
                },
                Loot = new[]
                {
                    new AuthoritativeWorldLootData { ItemId = "loot-1", ItemType = "weapon", Tier = "rare", X = 1, Y = 0 },
                },
                TerrainMesh = new AuthoritativeTerrainMeshData
                {
                    MeshId = "terrain-1",
                    Width = 2,
                    Height = 2,
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
                        new AuthoritativeTerrainMeshVertexData { X = 0f, Y = 0.25f, Z = 1f, U = 0f, V = 1f, NormalX = 0f, NormalY = 1f, NormalZ = 0f },
                        new AuthoritativeTerrainMeshVertexData { X = 1f, Y = 0.75f, Z = 1f, U = 1f, V = 1f, NormalX = 0f, NormalY = 1f, NormalZ = 0f },
                    },
                    Triangles = new[] { 0, 2, 1, 1, 2, 3 },
                },
            });

            _renderer.RefreshNow();

            var root = _gameObject.transform.Find("Authoritative World");
            Assert.IsNotNull(root);

            var terrain = root.Find("Terrain Mesh");
            Assert.IsNotNull(terrain);
            var mesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            Assert.IsNotNull(root.Find("Room 1"));
            Assert.IsNotNull(root.Find("Enemy 2"));
            Assert.IsNotNull(root.Find("Loot loot-1"));
        }
    }
}
