using NUnit.Framework;
using DunGen.Core;
using DunGen.Events;

namespace DunGen.Tests
{
    public class DeterminismTests
    {
        private DeterministicRNG _rng1;
        private DeterministicRNG _rng2;
        private const ulong TEST_SEED = 42;

        [SetUp]
        public void Setup()
        {
            _rng1 = new DeterministicRNG(TEST_SEED);
            _rng2 = new DeterministicRNG(TEST_SEED);
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            // Two RNGs with same seed should produce identical sequences
            for (int i = 0; i < 1000; i++)
            {
                float f1 = _rng1.NextFloat();
                float f2 = _rng2.NextFloat();
                Assert.AreEqual(f1, f2, $"Mismatch at iteration {i}");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var rng1 = new DeterministicRNG(42);
            var rng2 = new DeterministicRNG(43);

            // Sequences should differ (not deterministically identical)
            bool foundDifference = false;
            for (int i = 0; i < 100; i++)
            {
                if (rng1.NextFloat() != rng2.NextFloat())
                {
                    foundDifference = true;
                    break;
                }
            }
            Assert.IsTrue(foundDifference, "Different seeds should produce different sequences");
        }

        [Test]
        public void DiceRoll_RangeIsCorrect()
        {
            var rng = new DeterministicRNG(100);

            for (int i = 0; i < 1000; i++)
            {
                int roll = rng.DiceRoll(20);
                Assert.GreaterOrEqual(roll, 1, "D20 roll must be >= 1");
                Assert.LessOrEqual(roll, 20, "D20 roll must be <= 20");
            }
        }

        [Test]
        public void DiceRollMultiple_SumIsCorrect()
        {
            var rng = new DeterministicRNG(TEST_SEED);

            // 2d6 should be between 2 and 12
            for (int i = 0; i < 1000; i++)
            {
                int roll = rng.DiceRollMultiple(2, 6);
                Assert.GreaterOrEqual(roll, 2, "2d6 must be >= 2");
                Assert.LessOrEqual(roll, 12, "2d6 must be <= 12");
            }
        }

        [Test]
        public void Reset_ReturnsToPreviousState()
        {
            var rng = new DeterministicRNG(TEST_SEED);

            // Get some values
            float f1 = rng.NextFloat();
            float f2 = rng.NextFloat();
            float f3 = rng.NextFloat();

            // Reset
            rng.Reset();

            // Should get the same sequence again
            Assert.AreEqual(f1, rng.NextFloat());
            Assert.AreEqual(f2, rng.NextFloat());
            Assert.AreEqual(f3, rng.NextFloat());
        }

        [Test]
        public void NextInt_RangeIsCorrect()
        {
            var rng = new DeterministicRNG(TEST_SEED);

            for (int i = 0; i < 1000; i++)
            {
                int val = rng.NextInt(100);
                Assert.GreaterOrEqual(val, 0, "NextInt must be >= 0");
                Assert.Less(val, 100, "NextInt must be < max");
            }
        }

        [Test]
        public void NextIntWithRange_IsCorrect()
        {
            var rng = new DeterministicRNG(TEST_SEED);

            for (int i = 0; i < 1000; i++)
            {
                int val = rng.NextInt(50, 150);
                Assert.GreaterOrEqual(val, 50, "NextInt(50, 150) must be >= 50");
                Assert.Less(val, 150, "NextInt(50, 150) must be < 150");
            }
        }

        [Test]
        public void EventLog_RecordsEventsCorrectly()
        {
            var log = new EventLog();
            log.Initialize(TEST_SEED);

            var evt1 = new SimulationInitializedEventData 
            { 
                EventId = 1,
                Seed = TEST_SEED, 
                MaxEntities = 1000,
                FrameNumber = 0,
                Timestamp = 0f
            };
            log.RecordEvent(evt1);
            log.AdvanceFrame();

            Assert.AreEqual(1, log.GetEvents().Count);
            Assert.AreEqual(0, log.GetEvents()[0].FrameNumber);
        }

        [Test]
        public void EventLog_ExportsToJson()
        {
            var log = new EventLog();
            log.Initialize(TEST_SEED);

            var evt = new SimulationInitializedEventData 
            { 
                EventId = 1,
                Seed = TEST_SEED, 
                MaxEntities = 1000,
                FrameNumber = 0,
                Timestamp = 0f
            };
            log.RecordEvent(evt);

            string json = log.ExportToJson();
            Assert.IsTrue(json.Contains("\"seed\": 42"));
            Assert.IsTrue(json.Contains("SimulationInitialized"));
        }

        [Test]
        public void EventBus_PublishesAndSubscribes()
        {
            var bus = EventBus.Instance;
            bus.Clear();

            int callCount = 0;
            void Handler(SimulationInitializedEventData e) => callCount++;

            bus.Subscribe<SimulationInitializedEventData>(Handler);
            var evt = new SimulationInitializedEventData 
            { 
                EventId = 1,
                Seed = 42,
                MaxEntities = 100,
                FrameNumber = 0,
                Timestamp = 0f
            };
            bus.Publish(evt);

            Assert.AreEqual(1, callCount);
            bus.Unsubscribe<SimulationInitializedEventData>(Handler);
        }

        [Test]
        public void MultipleInitializations_ProduceDifferentResults()
        {
            var log1 = new EventLog();
            var log2 = new EventLog();

            log1.Initialize(42);
            log2.Initialize(43);

            Assert.AreNotEqual(log1.GetSeed(), log2.GetSeed());
        }
    }

    public class CombatDeterminismTests
    {
        [Test]
        public void TwentyDiceRolls_WithSameSeed_AreIdentical()
        {
            var rng1 = new DeterministicRNG(12345);
            var rng2 = new DeterministicRNG(12345);

            int[] rolls1 = new int[100];
            int[] rolls2 = new int[100];

            for (int i = 0; i < 100; i++)
            {
                rolls1[i] = rng1.DiceRoll(20);
                rolls2[i] = rng2.DiceRoll(20);
            }

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(rolls1[i], rolls2[i], $"Roll mismatch at index {i}");
            }
        }

        [Test]
        public void CombatSequence_IsDeterministic()
        {
            // Simulate a combat: player attacks goblin 3 times
            var rng1 = new DeterministicRNG(999);
            var rng2 = new DeterministicRNG(999);

            // Combat round 1: Player d20+5, Goblin AC 12
            int roll1_1 = rng1.DiceRoll(20) + 5;
            int roll2_1 = rng2.DiceRoll(20) + 5;
            Assert.AreEqual(roll1_1, roll2_1);

            // Combat round 2: Player d8+3 damage (if hit)
            int dmg1 = rng1.DiceRoll(8) + 3;
            int dmg2 = rng2.DiceRoll(8) + 3;
            Assert.AreEqual(dmg1, dmg2);

            // Combat round 3: Goblin attacks back, also d20
            int gobRoll1 = rng1.DiceRoll(20);
            int gobRoll2 = rng2.DiceRoll(20);
            Assert.AreEqual(gobRoll1, gobRoll2);
        }
    }
}
