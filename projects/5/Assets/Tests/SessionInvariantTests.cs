using System.Linq;
using DunGen.Config;
using DunGen.Core;
using DunGen.ECS.Core;
using DunGen.ECS.Models;
using DunGen.Events;
using DunGen.Gameplay;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Tests
{
    public class SessionInvariantTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
            EntityIndexCache.Instance.Clear();
            DirectEntityCache.Instance.Clear();
            SpatialHashGrid.Instance.Clear();
        }

        [Test]
        public void GameSession_SameSeedAndCommands_ProducesSameSnapshotAndDeterministicEventFrames()
        {
            var first = RunCommandStream();
            var second = RunCommandStream();

            AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
            CollectionAssert.AreEqual(first.EventFrames, second.EventFrames);
            CollectionAssert.AreEqual(first.EventTimestamps, second.EventTimestamps);
        }

        [Test]
        public void GameSession_Dispose_CleansTrackedEntitiesAndVisualRoot()
        {
            int rootsBefore = CountVisualPoolRoots();
            using (var session = CreateSession())
            {
                session.StartGame();
                Assert.Greater(session.GetTrackedEntityCountForDiagnostics(), 0);
                Assert.Greater(CountVisualPoolRoots(), rootsBefore);
            }

            Assert.AreEqual(0, EntityIndexCache.Instance.Count);
            Assert.AreEqual(rootsBefore, CountVisualPoolRoots());
        }

        [Test]
        public void EntityIndexCache_RegisteringEntityInNewSession_RemovesPreviousSessionMembership()
        {
            var cache = new EntityIndexCache();
            var entity = new Entity { Index = 42, Version = 1 };

            cache.Register(entity, 100);
            cache.Register(entity, 101);

            Assert.IsFalse(cache.TryGetSessionEntities(100, out var oldMembers) && oldMembers.Contains(entity.Index));
            Assert.IsTrue(cache.TryGetSessionEntities(101, out var newMembers));
            CollectionAssert.Contains(newMembers, entity.Index);
        }

        [Test]
        public void DirectEntityCache_RegisteringEntityInNewSession_RemovesPreviousSessionMembership()
        {
            var cache = new DirectEntityCache();
            var entity = new Entity { Index = 42, Version = 1 };

            cache.Register(entity, 100);
            cache.Register(entity, 101);

            Assert.AreEqual(0, cache.GetSessionEntityCount(100));
            Assert.AreEqual(1, cache.GetSessionEntityCount(101));
            Assert.AreEqual(entity.Index, cache.GetSessionEntityAt(101, 0));
        }

        private static (GameSnapshot Snapshot, uint[] EventFrames, float[] EventTimestamps) RunCommandStream()
        {
            EventBus.Instance.Clear();
            EntityIndexCache.Instance.Clear();
            SpatialHashGrid.Instance.Clear();

            using var session = CreateSession();
            session.StartGame();
            session.TryExecutePlayerCommand(PlayerTurnCommand.Wait(), out _);
            session.TryExecutePlayerCommand(PlayerTurnCommand.AttackNearest(), out _);
            session.TryExecutePlayerCommand(PlayerTurnCommand.Wait(), out _);

            var events = session.GetEventLog().GetEvents();
            return (
                new GameSnapshot(session.GetGameState(), session.GetLivingEnemyCountForClient(), session.IsGameOver, session.GameOverReason),
                events.Select(ReadFrameNumber).ToArray(),
                events.Select(ReadTimestamp).ToArray());
        }

        private static GameSession CreateSession()
        {
            var visualConfig = new VisualSpawnPoolConfig
            {
                DefaultInstancesPerArchetype = 0
            };
            return new GameSession(12345, visualConfig, new ModelAssetManifest(), EventBus.Instance, new FixedStepSimulationClock());
        }

        private static uint ReadFrameNumber(object evt)
        {
            var field = evt.GetType().GetField("FrameNumber");
            return field?.GetValue(evt) is uint frame ? frame : 0;
        }

        private static float ReadTimestamp(object evt)
        {
            var field = evt.GetType().GetField("Timestamp");
            return field?.GetValue(evt) is float timestamp ? timestamp : 0f;
        }

        private static int CountVisualPoolRoots()
        {
            return Object.FindObjectsOfType<GameObject>()
                .Count(obj => obj.name == "GameSession Visual Spawn Pool");
        }

        private static void AssertSnapshotsEqual(GameSnapshot expected, GameSnapshot actual)
        {
            Assert.AreEqual(expected.PlayerX, actual.PlayerX);
            Assert.AreEqual(expected.PlayerY, actual.PlayerY);
            Assert.AreEqual(expected.PlayerHealth, actual.PlayerHealth);
            Assert.AreEqual(expected.PlayerMaxHealth, actual.PlayerMaxHealth);
            Assert.AreEqual(expected.PlayerLevel, actual.PlayerLevel);
            Assert.AreEqual(expected.PlayerXP, actual.PlayerXP);
            Assert.AreEqual(expected.PlayerGold, actual.PlayerGold);
            Assert.AreEqual(expected.CurrentLevel, actual.CurrentLevel);
            Assert.AreEqual(expected.TurnCount, actual.TurnCount);
            Assert.AreEqual(expected.LivingEnemyCount, actual.LivingEnemyCount);
            Assert.AreEqual(expected.IsGameOver, actual.IsGameOver);
            Assert.AreEqual(expected.GameOverReason, actual.GameOverReason);
        }
    }
}
