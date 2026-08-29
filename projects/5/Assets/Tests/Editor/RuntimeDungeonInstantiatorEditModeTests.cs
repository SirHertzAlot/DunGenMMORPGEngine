using System.Collections;
using DunGen.Gameplay;
using DunGen.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DunGen.Tests.Editor
{
    public sealed class RuntimeDungeonInstantiatorEditModeTests
    {
        [Test]
        public void TryInstantiateDungeon_ReturnsFalse_WhenRuntimeInactive()
        {
            var go = new GameObject("RuntimeDungeonInstantiatorTest");
            var instantiator = go.AddComponent<RuntimeDungeonInstantiator>();
            var blueprint = CreateBlueprint();

            var result = instantiator.TryInstantiateDungeon(blueprint, isRuntimeActive: false);

            Assert.That(result, Is.False);
            Assert.That(instantiator.IsDungeonActive, Is.False);
            Assert.That(instantiator.transform.childCount, Is.EqualTo(0));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryInstantiateDungeon_ReturnsFalse_WhenBlueprintMissing()
        {
            var go = new GameObject("RuntimeDungeonInstantiatorTest");
            var instantiator = go.AddComponent<RuntimeDungeonInstantiator>();

            var result = instantiator.TryInstantiateDungeon(null, isRuntimeActive: true);

            Assert.That(result, Is.False);
            Assert.That(instantiator.IsDungeonActive, Is.False);
            Assert.That(instantiator.transform.childCount, Is.EqualTo(0));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryInstantiateDungeon_CreatesRuntimeMarkers_WhenRuntimeActive()
        {
            var go = new GameObject("RuntimeDungeonInstantiatorTest");
            var instantiator = go.AddComponent<RuntimeDungeonInstantiator>();
            var blueprint = CreateBlueprint();

            var result = instantiator.TryInstantiateDungeon(blueprint, isRuntimeActive: true);

            Assert.That(result, Is.True);
            Assert.That(instantiator.IsDungeonActive, Is.True);
            Assert.That(instantiator.transform.childCount, Is.EqualTo(3));

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator CleanupDungeon_DisablesRuntimeState_AndRemovesChildren()
        {
            var go = new GameObject("RuntimeDungeonInstantiatorTest");
            var instantiator = go.AddComponent<RuntimeDungeonInstantiator>();
            var blueprint = CreateBlueprint();

            instantiator.TryInstantiateDungeon(blueprint, isRuntimeActive: true);
            Assert.That(instantiator.transform.childCount, Is.GreaterThan(0));

            instantiator.CleanupDungeon();

            yield return null;

            Assert.That(instantiator.IsDungeonActive, Is.False);
            Assert.That(instantiator.transform.childCount, Is.EqualTo(0));

            Object.DestroyImmediate(go);
        }

        private static AuthoritativeWorldBlueprint CreateBlueprint()
        {
            return new AuthoritativeWorldBlueprint
            {
                Seed = 42,
                Width = 20,
                Height = 20,
                DungeonLevel = 1,
                Rooms =
                {
                    new AuthoritativeWorldRoomBlueprint
                    {
                        Id = 1,
                        X = 5,
                        Y = 5,
                        Width = 6,
                        Height = 6,
                    }
                },
                Enemies =
                {
                    new AuthoritativeWorldEnemyBlueprint
                    {
                        Id = 100,
                        Archetype = "test",
                        X = 6,
                        Y = 6,
                        Level = 1,
                    }
                },
                Loot =
                {
                    new AuthoritativeWorldLootBlueprint
                    {
                        ItemId = "loot_1",
                        ItemType = "gold",
                        Tier = "common",
                        X = 7,
                        Y = 7,
                    }
                }
            };
        }

        [Test]
        public void AuthoritativeWorldSceneRenderer_BuildsMeshColliderAndCalculatesSpawnPoint()
        {
            var go = new GameObject("TestWorldRendererHost");
            var stateStore = go.AddComponent<DunGen.Networking.AuthoritativeSessionStateStore>();
            var renderer = go.AddComponent<DunGen.Networking.AuthoritativeWorldSceneRenderer>();

            // Publish world received
            DunGen.Events.EventBus.Instance.Publish(new DunGen.Events.AuthoritativeWorldReceivedEventData
            {
                SessionId = "session-3d-test",
                ExecutionId = "exec-3d-1",
                Seed = 42,
                Width = 20,
                Height = 20,
                DungeonLevel = 1,
                Rooms = new[]
                {
                    new DunGen.Events.AuthoritativeWorldRoomData { Id = 1, X = 4, Y = 6, Width = 8, Height = 10 }
                },
                TerrainMesh = new DunGen.Events.AuthoritativeTerrainMeshData
                {
                    MeshId = "test-mesh",
                    Width = 2,
                    Height = 2,
                    Vertices = new[]
                    {
                        new DunGen.Events.AuthoritativeTerrainMeshVertexData { X = 0, Y = 0, Z = 0, NormalY = 1 },
                        new DunGen.Events.AuthoritativeTerrainMeshVertexData { X = 1, Y = 0, Z = 0, NormalY = 1 },
                        new DunGen.Events.AuthoritativeTerrainMeshVertexData { X = 0, Y = 0, Z = 1, NormalY = 1 },
                        new DunGen.Events.AuthoritativeTerrainMeshVertexData { X = 1, Y = 0, Z = 1, NormalY = 1 }
                    },
                    Triangles = new[] { 0, 2, 1, 1, 2, 3 }
                }
            });

            renderer.RefreshNow();

            var terrainObj = go.transform.Find("Authoritative World/Terrain Mesh");
            Assert.IsNotNull(terrainObj, "Terrain Mesh GameObject should be created");

            var meshCollider = terrainObj.GetComponent<MeshCollider>();
            Assert.IsNotNull(meshCollider, "MeshCollider should be attached to Terrain Mesh");
            Assert.IsNotNull(meshCollider.sharedMesh, "MeshCollider should have sharedMesh assigned");

            var spawnPos = renderer.GetPlayerSpawnPosition();
            Assert.AreEqual(8f, spawnPos.x, 0.01f); // 4 + (8 * 0.5f)
            Assert.AreEqual(11f, spawnPos.z, 0.01f); // 6 + (10 * 0.5f)
            Assert.Greater(spawnPos.y, 0f);

            Object.DestroyImmediate(go);
        }
    }
}
