using System;
using System.IO;
using System.Reflection;
using DunGen.Core;
using DunGen.Startup;
using NUnit.Framework;
using UnityEngine;

namespace DunGen.Tests.Editor
{
    public sealed class SimulationStarterReplayEditModeTests
    {
        private static readonly BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void ComputeReplayHash_ReturnsStableHexAndHandlesEmptyPayload()
        {
            var hashMethod = typeof(SimulationStarter).GetMethod("ComputeReplayHash", NonPublicStatic);
            Assert.That(hashMethod, Is.Not.Null);

            var empty = (string)hashMethod.Invoke(null, new object[] { string.Empty });
            var hashA = (string)hashMethod.Invoke(null, new object[] { "{\"events\":[1,2,3]}" });
            var hashB = (string)hashMethod.Invoke(null, new object[] { "{\"events\":[1,2,3]}" });

            Assert.That(empty, Is.EqualTo("empty"));
            Assert.That(hashA, Is.EqualTo(hashB));
            Assert.That(hashA.Length, Is.EqualTo(64));
        }

        [Test]
        public void WriteReplayLogToDisk_WritesPayload_AndPathContainsHash()
        {
            var writeMethod = typeof(SimulationStarter).GetMethod("WriteReplayLogToDisk", NonPublicStatic);
            Assert.That(writeMethod, Is.Not.Null);

            const string payload = "{\"frame\":1,\"events\":[]}";
            const string hash = "ABCDEF0123456789";

            var path = (string)writeMethod.Invoke(null, new object[] { payload, hash });

            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(path.StartsWith("export_failed:", StringComparison.Ordinal), Is.False);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo(payload));
            Assert.That(Path.GetFileName(path).Contains(hash, StringComparison.Ordinal), Is.True);

            File.Delete(path);
        }

        [Test]
        public void BuildMvpSmokeStatus_ReplayFlagTransitionsFromPendingToOk()
        {
            var go = new GameObject("SimulationStarterReplayTest");
            try
            {
                var starter = go.AddComponent<SimulationStarter>();
                var simulationField = typeof(SimulationStarter).GetField("_simulation", NonPublicInstance);
                var replayHashField = typeof(SimulationStarter).GetField("_lastReplayHash", NonPublicInstance);
                var statusMethod = typeof(SimulationStarter).GetMethod("BuildMvpSmokeStatus", NonPublicInstance);

                Assert.That(simulationField, Is.Not.Null);
                Assert.That(replayHashField, Is.Not.Null);
                Assert.That(statusMethod, Is.Not.Null);

                var simulation = new DunGen.Core.Simulation();
                simulation.Initialize(100UL);
                simulationField.SetValue(starter, simulation);

                replayHashField.SetValue(starter, "n/a");
                var pending = (string)statusMethod.Invoke(starter, null);

                replayHashField.SetValue(starter, "A1");
                var ok = (string)statusMethod.Invoke(starter, null);

                Assert.That(pending, Does.Contain("replay:pending"));
                Assert.That(ok, Does.Contain("replay:ok"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
