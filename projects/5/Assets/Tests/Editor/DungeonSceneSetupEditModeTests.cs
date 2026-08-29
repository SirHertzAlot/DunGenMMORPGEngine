using DunGen.Startup;
using DunGen.Testing.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DunGen.Tests.Editor
{
    public sealed class DungeonSceneSetupEditModeTests
    {
        [TearDown]
        public void TearDown()
        {
            var starter = Object.FindAnyObjectByType<SimulationStarter>();
            if (starter != null)
            {
                Object.DestroyImmediate(starter.gameObject);
            }

            var fallback = GameObject.Find("POLYGON_Dungeons_Demo_Scene");
            if (fallback != null)
            {
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void CreateFallbackDungeonMarker_CreatesVisibleMarkerRoot()
        {
            var root = DungeonSceneSetup.CreateFallbackDungeonMarkerForTests();

            Assert.That(root, Is.Not.Null);
            Assert.That(root.name, Is.EqualTo("POLYGON_Dungeons_Demo_Scene"));
            Assert.That(root.transform.childCount, Is.EqualTo(1));

            var marker = root.transform.GetChild(0).gameObject;
            Assert.That(marker.name, Is.EqualTo("MissingDungeonFbxMarker"));

            var renderer = marker.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.color, Is.EqualTo(Color.red));
        }

        [Test]
        public void EnsureSimulationStarter_CreatesSingleStarterObject()
        {
            DungeonSceneSetup.EnsureSimulationStarterForTests();
            DungeonSceneSetup.EnsureSimulationStarterForTests();

            var starters = Object.FindObjectsByType<SimulationStarter>(FindObjectsInactive.Include);
            Assert.That(starters.Length, Is.EqualTo(1));
            Assert.That(starters[0].gameObject.name, Is.EqualTo("DunGen Simulation Starter"));
        }
    }
}
