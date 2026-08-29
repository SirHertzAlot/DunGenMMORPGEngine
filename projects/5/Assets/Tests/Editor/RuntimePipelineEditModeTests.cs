using System;
using DunGen.Core;
using DunGen.ECS.Components;
using DunGen.ECS.Core;
using DunGen.Events;
using DunGen.Events.Combat;
using NUnit.Framework;
using Unity.Entities;

namespace DunGen.Tests.Editor
{
    public sealed class RuntimePipelineEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Instance.Clear();
            DirectEntityCache.Instance.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Instance.Clear();
            DirectEntityCache.Instance.Clear();
        }

        [Test]
        public void EventBus_Publish_InvokesTypedAndAnyEventSubscribers()
        {
            var bus = EventBus.Instance;

            CombatStartedEventData typedPayload = default;
            var typedHit = false;
            Type anyType = null;
            object anyPayload = null;

            bus.Subscribe<CombatStartedEventData>(evt =>
            {
                typedHit = true;
                typedPayload = evt;
            });

            bus.AnyEventPublished += (eventType, payload) =>
            {
                anyType = eventType;
                anyPayload = payload;
            };

            var expected = new CombatStartedEventData
            {
                EventId = bus.GetNextEventId(),
                FrameNumber = 42,
                CombatSessionId = 123,
                Timestamp = 1.5f,
                IsBossEncounter = false,
                ParticipantCount = 2
            };

            bus.Publish(expected);

            Assert.That(typedHit, Is.True);
            Assert.That(typedPayload.CombatSessionId, Is.EqualTo(123));
            Assert.That(anyType, Is.EqualTo(typeof(CombatStartedEventData)));
            Assert.That(anyPayload, Is.Not.Null);
        }

        [Test]
        public void EventBus_GetNextEventId_IncrementsSequentially()
        {
            var bus = EventBus.Instance;
            var first = bus.GetNextEventId();
            var second = bus.GetNextEventId();

            Assert.That(second, Is.EqualTo(first + 1));
        }

        [Test]
        public void Simulation_InitializeAndStep_EmitsInitAndAdvancesFrames()
        {
            var simulation = new DunGen.Core.Simulation();
            var bus = EventBus.Instance;

            var initReceived = false;
            SimulationInitializedEventData initPayload = default;
            bus.Subscribe<SimulationInitializedEventData>(evt =>
            {
                initReceived = true;
                initPayload = evt;
            });

            simulation.Initialize(987654321UL);
            simulation.SimulationStep((1f / 60f) * 3.1f);

            Assert.That(initReceived, Is.True);
            Assert.That(initPayload.Seed, Is.EqualTo(987654321UL));
            Assert.That(simulation.GetFrameNumber(), Is.GreaterThanOrEqualTo(3));
            Assert.That(simulation.IsRunning, Is.True);
        }

        [Test]
        public void Simulation_CreateEntity_AssignsCoreComponentsAndPublishesEvent()
        {
            var simulation = new DunGen.Core.Simulation();
            var bus = EventBus.Instance;
            simulation.Initialize(123UL);

            var createdReceived = false;
            EntityCreatedEventData createdPayload = default;
            bus.Subscribe<EntityCreatedEventData>(evt =>
            {
                createdReceived = true;
                createdPayload = evt;
            });

            var created = simulation.CreateEntity("unit-test-player", new Vector3(3f, 4f, 5f));
            var em = simulation.GetEntityManager();

            Assert.That(createdReceived, Is.True);
            Assert.That(createdPayload.Name, Is.EqualTo("unit-test-player"));
            Assert.That(em.Exists(created), Is.True);
            Assert.That(em.HasComponent<Position>(created), Is.True);
            Assert.That(em.HasComponent<Name>(created), Is.True);

            var position = em.GetComponentData<Position>(created);
            Assert.That(position.X, Is.EqualTo(3f));
            Assert.That(position.Y, Is.EqualTo(4f));
            Assert.That(position.Z, Is.EqualTo(5f));
        }

        [Test]
        public void DirectEntityCache_RegisterPlayerAndSession_MaintainsFastLookups()
        {
            var cache = DirectEntityCache.Instance;
            var player = new Entity { Index = 101, Version = 1 };

            cache.RegisterPlayer(player);
            cache.Register(player, combatSessionId: 3);

            var found = cache.TryGetPlayerEntity(out var foundPlayer);

            Assert.That(found, Is.True);
            Assert.That(foundPlayer.Index, Is.EqualTo(player.Index));
            Assert.That(cache.GetSessionEntityCount(3), Is.EqualTo(1));
            Assert.That(cache.GetSessionEntityAt(3, 0), Is.EqualTo(player.Index));
            Assert.That(cache.HasFlag(player.Index, DirectEntityCache.EntityFlags.InCombat), Is.True);
            Assert.That(cache.Contains(player.Index), Is.True);
        }

        [Test]
        public void ActionQueueComponent_CanBeStoredAndReadFromEntity()
        {
            using var world = new World("ActionQueueComponentTestWorld");
            var em = world.EntityManager;

            var entity = em.CreateEntity(typeof(ActionQueue));
            var expected = new ActionQueue
            {
                ActionType = 7,
                Priority = 99,
                FrameQueued = 250
            };

            em.SetComponentData(entity, expected);
            var actual = em.GetComponentData<ActionQueue>(entity);

            Assert.That(actual.ActionType, Is.EqualTo(expected.ActionType));
            Assert.That(actual.Priority, Is.EqualTo(expected.Priority));
            Assert.That(actual.FrameQueued, Is.EqualTo(expected.FrameQueued));
        }
    }
}
