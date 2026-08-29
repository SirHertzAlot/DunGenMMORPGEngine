using System.Collections.Generic;
using DunGen.Events;
using DunGen.Networking;
using DunGen.Gameplay;
using DunGen.Testing;
using NUnit.Framework;
using UnityEngine;

namespace DunGen.Tests
{
    public class GameplayObservabilityAutomationTests
    {
        private GameObject _gameObject;
        private BackendObservabilityBridge _bridge;

        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
            _gameObject = new GameObject("GameplayObservabilityAutomationTests");
            _bridge = _gameObject.AddComponent<BackendObservabilityBridge>();
            _bridge.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridge != null)
                _bridge.Unsubscribe();

            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);

            EventBus.Instance.Clear();
        }

        [Test]
        public void AutomationScript_WhenCombatStarts_QueuesObservabilityLogEvent()
        {
            var queuedEvents = new List<WorldSessionEventIngestDto>();
            _bridge.EventQueuedForPost += queuedEvents.Add;

            var runner = new GameplayAutomationRunner();
            var result = runner.Run(CreateAdjacentEnemyScript(), null, automationResult => queuedEvents.Count > 0);

            Assert.Greater(result.ExecutedTurns, 0);
            Assert.IsNotEmpty(queuedEvents);

            var combatEvent = queuedEvents.Find(evt =>
                IsAnyOf(evt.eventType, "combat.started", "ecs.combat-started"));

            Assert.IsNotNull(combatEvent);
            Assert.AreEqual("combat", combatEvent.category);
            Assert.That(combatEvent.message, Does.Match("Combat session|CombatStarted"));
        }

        [Test]
        public void AutomationScript_WhenEnemyFalls_QueuesLootAndLevelUpObservabilityEvents()
        {
            var queuedEvents = new List<WorldSessionEventIngestDto>();
            _bridge.EventQueuedForPost += queuedEvents.Add;

            var runner = new GameplayAutomationRunner();
            var result = runner.Run(
                CreateAdjacentEnemyScript(),
                session =>
                {
                    Assert.Greater(session.DebugSetLivingEnemyHealth(1), 0);
                    Assert.IsTrue(session.DebugSetPlayerExperience(99, 1, 100));
                },
                automationResult =>
                    queuedEvents.Exists(evt => IsAnyOf(evt.eventType, "progression.loot.granted", "ecs.loot-granted"))
                    && queuedEvents.Exists(evt => IsAnyOf(evt.eventType, "progression.level.up", "ecs.level-up")));

            Assert.Greater(result.ExecutedTurns, 0);
            Assert.That(queuedEvents, Has.Some.Matches<WorldSessionEventIngestDto>(evt =>
                IsAnyOf(evt.eventType, "progression.loot.granted", "ecs.loot-granted")));
            Assert.That(queuedEvents, Has.Some.Matches<WorldSessionEventIngestDto>(evt =>
                IsAnyOf(evt.eventType, "progression.level.up", "ecs.level-up")));
        }

        private static bool IsAnyOf(string value, params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(value) || candidates == null)
                return false;

            for (var i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(value, candidates[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static GameplayAutomationScript CreateAdjacentEnemyScript()
        {
            var script = ScriptableObject.CreateInstance<GameplayAutomationScript>();
            script.Seed = 4242;
            script.MaxTurns = 2;
            script.AuthoritativeWorld = new AuthoritativeWorldBlueprint
            {
                Seed = 4242,
                Width = 80,
                Height = 24,
                DungeonLevel = 1,
                Rooms = new List<AuthoritativeWorldRoomBlueprint>
                {
                    new AuthoritativeWorldRoomBlueprint
                    {
                        Id = 1,
                        X = 35,
                        Y = 8,
                        Width = 12,
                        Height = 8,
                    },
                },
                Enemies = new List<AuthoritativeWorldEnemyBlueprint>
                {
                    new AuthoritativeWorldEnemyBlueprint
                    {
                        Id = 101,
                        Archetype = "goblin",
                        X = 40,
                        Y = 12,
                        Level = 1,
                    },
                },
            };

            return script;
        }
    }
}