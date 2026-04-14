using DunGen.Events;
using DunGen.Events.Combat;
using NUnit.Framework;
using System.Collections.Generic;

namespace DunGen.Tests
{
    /// <summary>
    /// Functional tests for data-oriented ECS event system.
    /// Verifies struct-based events work correctly (no OOP patterns).
    /// </summary>
    public class DataOrientedEventSystemTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
        }

        #region Basic Event Data Tests

        [Test]
        public void SimulationInitializedEventData_CanBeCreated_WithAllFields()
        {
            // Arrange & Act
            var evt = new SimulationInitializedEventData
            {
                EventId = 1,
                FrameNumber = 0,
                Timestamp = 0f,
                Seed = 12345,
                MaxEntities = 1000
            };

            // Assert
            Assert.AreEqual(1, evt.EventId);
            Assert.AreEqual(12345, evt.Seed);
            Assert.AreEqual(1000, evt.MaxEntities);
        }

        [Test]
        public void CombatStartedEventData_CanBeCreated_WithArrays()
        {
            // Arrange
            int[] participants = { 1, 2, 3 };
            int[] initiative = { 3, 1, 2 };

            // Act
            var evt = new CombatStartedEventData
            {
                EventId = 1,
                FrameNumber = 100,
                Timestamp = 1.667f,
                ParticipantEntityIds = participants,
                InitiativeOrder = initiative,
                CombatSessionId = 42
            };

            // Assert
            Assert.AreEqual(3, evt.ParticipantEntityIds.Length);
            Assert.AreEqual(42, evt.CombatSessionId);
        }

        #endregion

        #region EventBus Struct Dispatch Tests

        [Test]
        public void EventBus_Publish_SimpleEvent_IsReceived()
        {
            // Arrange
            var bus = EventBus.Instance;
            bool received = false;

            bus.Subscribe<AttackEventData>(evt =>
            {
                received = true;
            });

            var evt_data = new AttackEventData
            {
                EventId = 1,
                FrameNumber = 10,
                Timestamp = 0.167f,
                SourceEntity = default,
                TargetEntity = default,
                AttackRoll = 15,
                DamageRoll = 7,
                Hit = true,
                TargetAC = 12
            };

            // Act
            bus.Publish(evt_data);

            // Assert
            Assert.IsTrue(received, "Event subscriber should be called");
        }

        [Test]
        public void EventBus_Publish_MultipleTypes_IndependentSubscriptions()
        {
            // Arrange
            var bus = EventBus.Instance;
            int attackCount = 0;
            int damageCount = 0;

            bus.Subscribe<AttackEventData>(_ => attackCount++);
            bus.Subscribe<DamageTakenEventData>(_ => damageCount++);

            // Act
            bus.Publish(new AttackEventData
            {
                EventId = 1,
                FrameNumber = 1,
                Timestamp = 0.016f,
                SourceEntity = default,
                TargetEntity = default,
                AttackRoll = 10,
                DamageRoll = 5,
                Hit = true,
                TargetAC = 10
            });

            bus.Publish(new DamageTakenEventData
            {
                EventId = 2,
                FrameNumber = 2,
                Timestamp = 0.032f,
                SourceEntity = default,
                DamageAmount = 5,
                RemainingHealth = 45,
                MaxHealth = 50
            });

            // Assert
            Assert.AreEqual(1, attackCount, "Attack event should be received once");
            Assert.AreEqual(1, damageCount, "Damage event should be received once");
        }

        [Test]
        public void EventBus_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            var bus = EventBus.Instance;
            int subscriber1Count = 0;
            int subscriber2Count = 0;
            int subscriber3Count = 0;

            bus.Subscribe<EntityCreatedEventData>(_ => subscriber1Count++);
            bus.Subscribe<EntityCreatedEventData>(_ => subscriber2Count++);
            bus.Subscribe<EntityCreatedEventData>(_ => subscriber3Count++);

            var evt = new EntityCreatedEventData
            {
                EventId = 1,
                FrameNumber = 1,
                Timestamp = 0.016f,
                SourceEntity = default,
                EntityType = "Orc",
                Name = "Grommash"
            };

            // Act
            bus.Publish(evt);

            // Assert
            Assert.AreEqual(1, subscriber1Count, "First subscriber");
            Assert.AreEqual(1, subscriber2Count, "Second subscriber");
            Assert.AreEqual(1, subscriber3Count, "Third subscriber");
        }

        [Test]
        public void EventBus_Unsubscribe_StopsReceivingEvents()
        {
            // Arrange
            var bus = EventBus.Instance;
            int count = 0;
            void Handler(EntityMovedEventData _) => count++;

            bus.Subscribe<EntityMovedEventData>(Handler);

            var evt = new EntityMovedEventData
            {
                EventId = 1,
                FrameNumber = 1,
                Timestamp = 0.016f,
                SourceEntity = default,
                FromX = 0,
                FromY = 0,
                ToX = 1,
                ToY = 1
            };

            // Act 1 - Subscribe and publish
            bus.Publish(evt);
            Assert.AreEqual(1, count);

            // Act 2 - Unsubscribe and publish
            bus.Unsubscribe<EntityMovedEventData>(Handler);
            bus.Publish(evt);

            // Assert - Count should not increase
            Assert.AreEqual(1, count, "Should not receive after unsubscribe");
        }

        #endregion

        #region Struct Value Type Tests

        [Test]
        public void EventData_IsValueType_CopiesAreIndependent()
        {
            // Arrange
            var evt1 = new SimulationInitializedEventData
            {
                EventId = 1,
                FrameNumber = 0,
                Timestamp = 0f,
                Seed = 12345,
                MaxEntities = 1000
            };

            // Act
            var evt2 = evt1;
            evt2.Seed = 54321;

            // Assert - Original unchanged (value type behavior)
            Assert.AreEqual(12345, evt1.Seed, "Original struct should be unchanged");
            Assert.AreEqual(54321, evt2.Seed, "Copied struct should have new value");
        }

        [Test]
        public void EventData_PassedToMethod_ByValue_NotModified()
        {
            // Arrange
            var evt = new DamageTakenEventData
            {
                EventId = 1,
                FrameNumber = 5,
                Timestamp = 0.083f,
                SourceEntity = default,
                DamageAmount = 10,
                RemainingHealth = 40,
                MaxHealth = 50
            };

            // Act
            ModifyEvent(evt);

            // Assert - Original unchanged (passed by value)
            Assert.AreEqual(10, evt.DamageAmount, "Original should be unchanged");
        }

        private void ModifyEvent(DamageTakenEventData evt)
        {
            evt.DamageAmount = 999;  // Modifies copy, not original
        }

        #endregion

        #region Combat Event Tests

        [Test]
        public void CombatStartedEventData_HasCorrectStructure()
        {
            // Arrange & Act
            var evt = new CombatStartedEventData
            {
                EventId = 1,
                FrameNumber = 50,
                Timestamp = 0.833f,
                ParticipantEntityIds = new int[] { 1, 2 },
                InitiativeOrder = new int[] { 2, 1 },
                CombatSessionId = 99
            };

            // Assert
            Assert.IsNotNull(evt.ParticipantEntityIds);
            Assert.IsNotNull(evt.InitiativeOrder);
            Assert.AreEqual(2, evt.ParticipantEntityIds.Length);
        }

        [Test]
        public void AttackResolvedEventData_TracksFullAttackData()
        {
            // Arrange & Act
            var evt = new AttackResolvedEventData
            {
                EventId = 1,
                FrameNumber = 100,
                Timestamp = 1.667f,
                AttackerEntityId = 5,
                DefenderEntityId = 10,
                D20Roll = 18,
                AttackModifier = 3,
                TargetAC = 15,
                FinalAttackRoll = 21,
                IsHit = true,
                IsNaturalTwenty = false,
                IsNaturalOne = false,
                WeaponName = "Longsword",
                DamageIfHit = 8
            };

            // Assert
            Assert.IsTrue(evt.IsHit);
            Assert.AreEqual(21, evt.FinalAttackRoll);
            Assert.AreEqual(8, evt.DamageIfHit);
        }

        [Test]
        public void DamageInflictedEventData_TracksAllDamageInfo()
        {
            // Arrange & Act
            var evt = new DamageInflictedEventData
            {
                EventId = 1,
                FrameNumber = 101,
                Timestamp = 1.683f,
                VictimEntityId = 10,
                DamageDealt = 8,
                DamageType = "Slashing",
                DamageMultiplier = 1.0f,
                BaseDamage = 8,
                DamageSource = "Longsword",
                VictimHealthRemaining = 22
            };

            // Assert
            Assert.AreEqual(8, evt.DamageDealt);
            Assert.AreEqual("Slashing", evt.DamageType);
            Assert.AreEqual(22, evt.VictimHealthRemaining);
        }

        #endregion

        #region EventLog Tests

        [Test]
        public void EventLog_RecordsStructEvent_Correctly()
        {
            // Arrange
            var log = new EventLog();
            log.Initialize(42);

            var evt = new EntityCreatedEventData
            {
                EventId = 1,
                FrameNumber = 1,
                Timestamp = 0.016f,
                SourceEntity = default,
                EntityType = "Goblin",
                Name = "Grok"
            };

            // Act
            log.RecordEvent(evt);

            // Assert
            var events = log.GetEvents();
            Assert.AreEqual(1, events.Count, "Event should be recorded");
        }

        [Test]
        public void EventLog_ExportToJson_SerializesStructData()
        {
            // Arrange
            var log = new EventLog();
            log.Initialize(100);

            var evt = new SimulationInitializedEventData
            {
                EventId = 1,
                FrameNumber = 0,
                Timestamp = 0f,
                Seed = 100,
                MaxEntities = 500
            };

            // Act
            log.RecordEvent(evt);
            string json = log.ExportToJson();

            // Assert
            Assert.IsTrue(json.Contains("\"seed\": 100"), "JSON should contain seed");
            Assert.IsTrue(json.Contains("SimulationInitialized"), "JSON should contain event type");
        }

        #endregion

        #region Integration Tests

        [Test]
        public void CompleteEventFlow_EventBusToLog()
        {
            // Arrange
            var bus = EventBus.Instance;
            var log = new EventLog();
            log.Initialize(50);

            List<object> receivedEvents = new();
            bus.Subscribe<CombatStartedEventData>(evt => receivedEvents.Add(evt));

            var combatEvent = new CombatStartedEventData
            {
                EventId = bus.GetNextEventId(),
                FrameNumber = 10,
                Timestamp = 0.167f,
                ParticipantEntityIds = new int[] { 1, 2, 3 },
                InitiativeOrder = new int[] { 3, 1, 2 },
                CombatSessionId = 1
            };

            // Act
            bus.Publish(combatEvent);
            log.RecordEvent(combatEvent);

            // Assert
            Assert.AreEqual(1, receivedEvents.Count, "Event should be published");
            Assert.AreEqual(1, log.GetEvents().Count, "Event should be logged");
        }

        [Test]
        public void MultipleEventTypes_AllWorkTogether()
        {
            // Arrange
            var bus = EventBus.Instance;
            var counts = new Dictionary<string, int>
            {
                { "init", 0 },
                { "combat", 0 },
                { "attack", 0 },
                { "damage", 0 }
            };

            bus.Subscribe<SimulationInitializedEventData>(_ => counts["init"]++);
            bus.Subscribe<CombatStartedEventData>(_ => counts["combat"]++);
            bus.Subscribe<AttackResolvedEventData>(_ => counts["attack"]++);
            bus.Subscribe<DamageInflictedEventData>(_ => counts["damage"]++);

            // Act
            bus.Publish(new SimulationInitializedEventData { EventId = 1, Seed = 1, MaxEntities = 100, FrameNumber = 0, Timestamp = 0f });
            bus.Publish(new CombatStartedEventData { EventId = 2, CombatSessionId = 1, FrameNumber = 1, Timestamp = 0.016f });
            bus.Publish(new AttackResolvedEventData { EventId = 3, AttackerEntityId = 1, DefenderEntityId = 2, FrameNumber = 2, Timestamp = 0.032f });
            bus.Publish(new DamageInflictedEventData { EventId = 4, VictimEntityId = 2, DamageDealt = 5, FrameNumber = 3, Timestamp = 0.048f });

            // Assert
            Assert.AreEqual(1, counts["init"], "Simulation init event");
            Assert.AreEqual(1, counts["combat"], "Combat start event");
            Assert.AreEqual(1, counts["attack"], "Attack event");
            Assert.AreEqual(1, counts["damage"], "Damage event");
        }

        #endregion
    }
}
