using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DunGen.Events;
using DunGen.Networking;

namespace DunGen.Tests
{
    [TestFixture]
    public class BinarySerializationEditModeTests
    {
        [Test]
        public void BinaryWorldSnapshot_RoundtripsSuccessfully()
        {
            var worldData = new AuthoritativeWorldReceivedEventData
            {
                SessionId = "session-test-101",
                ExecutionId = "exec-abc-999",
                Seed = 1337,
                Width = 100,
                Height = 60,
                DungeonLevel = 4,
                Rooms = new[]
                {
                    new AuthoritativeWorldRoomData { Id = 1, X = 10, Y = 12, Width = 20, Height = 15 },
                    new AuthoritativeWorldRoomData { Id = 2, X = 40, Y = 30, Width = 25, Height = 20 }
                },
                Enemies = new[]
                {
                    new AuthoritativeWorldEnemyData { Id = 101, Archetype = "goblin_archer", X = 15, Y = 14, Level = 3 },
                    new AuthoritativeWorldEnemyData { Id = 102, Archetype = "orc_warrior", X = 45, Y = 32, Level = 5 }
                },
                Loot = new[]
                {
                    new AuthoritativeWorldLootData { ItemId = "item_sword_1", ItemType = "sword", Tier = "rare", X = 16, Y = 15 },
                    new AuthoritativeWorldLootData { ItemId = "item_potion_1", ItemType = "potion", Tier = "common", X = 46, Y = 33 }
                },
                TerrainMesh = new AuthoritativeTerrainMeshData
                {
                    MeshId = "mesh-001",
                    Width = 100,
                    Height = 60,
                    Seed = 1337,
                    Algorithm = "PerlinSimplex",
                    WaterLevel = 0.2f,
                    HeightScale = 1.5f,
                    MinHeight = 0f,
                    MaxHeight = 10f,
                    Vertices = new[]
                    {
                        new AuthoritativeTerrainMeshVertexData { X = 0f, Y = 1f, Z = 2f, U = 0f, V = 0f, NormalX = 0f, NormalY = 1f, NormalZ = 0f },
                        new AuthoritativeTerrainMeshVertexData { X = 1f, Y = 2f, Z = 3f, U = 1f, V = 1f, NormalX = 0f, NormalY = 1f, NormalZ = 0f }
                    },
                    Triangles = new[] { 0, 1, 0 }
                }
            };

            var bytes = BinaryWorldSnapshotCodec.SerializeWorld(worldData);
            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 32);

            bool success = BinaryWorldSnapshotCodec.TryDeserializeWorld(bytes, out var deserialized, out var error);
            Assert.IsTrue(success, $"Deserialization failed: {error}");
            Assert.IsNull(error);

            Assert.AreEqual(worldData.SessionId, deserialized.SessionId);
            Assert.AreEqual(worldData.ExecutionId, deserialized.ExecutionId);
            Assert.AreEqual(worldData.Seed, deserialized.Seed);
            Assert.AreEqual(worldData.Width, deserialized.Width);
            Assert.AreEqual(worldData.Height, deserialized.Height);
            Assert.AreEqual(worldData.DungeonLevel, deserialized.DungeonLevel);

            Assert.AreEqual(worldData.Rooms.Length, deserialized.Rooms.Length);
            Assert.AreEqual(worldData.Rooms[0].Id, deserialized.Rooms[0].Id);
            Assert.AreEqual(worldData.Rooms[0].Width, deserialized.Rooms[0].Width);

            Assert.AreEqual(worldData.Enemies.Length, deserialized.Enemies.Length);
            Assert.AreEqual(worldData.Enemies[0].Archetype, deserialized.Enemies[0].Archetype);
            Assert.AreEqual(worldData.Enemies[1].Level, deserialized.Enemies[1].Level);

            Assert.AreEqual(worldData.Loot.Length, deserialized.Loot.Length);
            Assert.AreEqual(worldData.Loot[0].ItemId, deserialized.Loot[0].ItemId);
            Assert.AreEqual(worldData.Loot[0].Tier, deserialized.Loot[0].Tier);

            Assert.AreEqual(worldData.TerrainMesh.MeshId, deserialized.TerrainMesh.MeshId);
            Assert.AreEqual(worldData.TerrainMesh.Vertices.Length, deserialized.TerrainMesh.Vertices.Length);
            Assert.AreEqual(worldData.TerrainMesh.Triangles.Length, deserialized.TerrainMesh.Triangles.Length);
        }

        [Test]
        public void BinaryEntitySnapshot_RoundtripsSuccessfully()
        {
            var entities = new[]
            {
                new BinaryWorldSnapshotCodec.BinaryEntityState
                {
                    EntityId = 1,
                    EntityType = 0, // Player
                    X = 12,
                    Y = 18,
                    Health = 100,
                    MaxHealth = 100,
                    Flags = 1 // InCombat
                },
                new BinaryWorldSnapshotCodec.BinaryEntityState
                {
                    EntityId = 50,
                    EntityType = 1, // Enemy
                    X = 14,
                    Y = 18,
                    Health = 45,
                    MaxHealth = 50,
                    Flags = 1 // InCombat
                }
            };

            var bytes = BinaryWorldSnapshotCodec.SerializeEntityBatch(42, 10.5f, entities);
            Assert.IsNotNull(bytes);

            bool success = BinaryWorldSnapshotCodec.TryDeserializeEntityBatch(bytes, out var batch, out var error);
            Assert.IsTrue(success, $"Entity batch deserialization failed: {error}");
            Assert.IsNull(error);

            Assert.AreEqual(42u, batch.FrameNumber);
            Assert.AreEqual(10.5f, batch.Timestamp, 0.001f);
            Assert.AreEqual(2, batch.Entities.Length);
            Assert.AreEqual(1, batch.Entities[0].EntityId);
            Assert.AreEqual(100, batch.Entities[0].Health);
            Assert.AreEqual(50, batch.Entities[1].EntityId);
            Assert.AreEqual(45, batch.Entities[1].Health);
        }

        [Test]
        public void InvalidBinaryPayload_FailsGracefully()
        {
            var corrupted = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            bool success = BinaryWorldSnapshotCodec.TryDeserializeWorld(corrupted, out _, out var error);
            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }
    }
}
