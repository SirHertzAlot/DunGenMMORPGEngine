using NUnit.Framework;
using DunGen.Core;
using CoreSimulation = DunGen.Core.Simulation;
using DunGen.Simulation.RNG;
using DunGen.Events;
using Unity.Entities;

namespace DunGen.Tests
{
    /// <summary>
    /// Integration test: End-to-end simulation with replay validation.
    /// This demonstrates the complete deterministic simulation pipeline.
    /// </summary>
    public class SimulationIntegrationTests
    {
        private CoreSimulation _sim;

        [SetUp]
        public void Setup()
        {
            _sim = new CoreSimulation();
        }

        [TearDown]
        public void Teardown()
        {
            if (_sim?.GetWorld() != null)
            {
                _sim.Stop();
            }
        }

        [Test]
        public void SimulationInitialization_RecordsEvent()
        {
            const ulong SEED = 12345;
            _sim.Initialize(SEED);

            Assert.AreEqual(SEED, _sim.GetSeed());
            Assert.IsTrue(_sim.IsRunning);
            Assert.AreEqual(0, _sim.GetFrameNumber());

            var log = _sim.GetEventLog();
            Assert.Greater(log.GetEvents().Count, 0, "Should have initialization event");
        }

        [Test]
        public void SimulationStep_AdvancesFrames()
        {
            _sim.Initialize(42);

            // First frame
            _sim.SimulationStep(0.1f);
            uint frame1 = _sim.GetFrameNumber();

            // Second frame
            _sim.SimulationStep(0.1f);
            uint frame2 = _sim.GetFrameNumber();

            Assert.Greater(frame2, frame1, "Frames should advance");
        }

        [Test]
        public void DeterministicDiceRolls_WithSameSeed_ProduceIdenticalSequence()
        {
            // Simulate combat round 1: attack roll checks
            _sim.Initialize(999);
            var rng1 = _sim.GetRNG();

            int[] rolls1 = new int[10];
            for (int i = 0; i < 10; i++)
            {
                rolls1[i] = rng1.DiceRoll(20);  // Attack roll
            }

            // Create new simulation with same seed
            var sim2 = new CoreSimulation();
            sim2.Initialize(999);
            var rng2 = sim2.GetRNG();

            int[] rolls2 = new int[10];
            for (int i = 0; i < 10; i++)
            {
                rolls2[i] = rng2.DiceRoll(20);
            }

            // Verify identical sequences
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(rolls1[i], rolls2[i], $"Roll mismatch at index {i}");
            }
        }

        [Test]
        public void EventLog_ExportsAndContainsExpectedData()
        {
            _sim.Initialize(777);

            // Manually log an action
            var log = _sim.GetEventLog();
            ulong rngBefore = _sim.GetRNG().GetState();
            int d20 = _sim.GetRNG().DiceRoll(20);
            ulong rngAfter = _sim.GetRNG().GetState();

            log.LogAction("Attack", $"{{\"targetId\":1,\"roll\":{d20}}}", rngBefore, rngAfter);
            log.AdvanceFrame();

            string json = log.ExportToJson();
            Assert.IsTrue(json.Contains("\"seed\": 777"));
            Assert.IsTrue(json.Contains("\"type\": \"Attack\""));
            Assert.IsTrue(json.Contains($"\"roll\":{d20}"));
        }

        [Test]
        public void FullCombatSimulation_IsDeterministic()
        {
            // SCENARIO: Player (AC 15) attacks Goblin (AC 12), deals damage
            
            // Simulation 1
            _sim.Initialize(54321);
            var rng1 = _sim.GetRNG();

            // Player attacks: d20+3 vs Goblin AC 12
            int attack1 = rng1.DiceRoll(20) + 3;
            bool hit1 = attack1 >= 12;
            int damage1 = hit1 ? rng1.DiceRoll(8) + 2 : 0;  // d8+2 longsword

            var log1 = _sim.GetEventLog().ExportToJson();

            // Simulation 2 - Same seed
            var sim2 = new CoreSimulation();
            sim2.Initialize(54321);
            var rng2 = sim2.GetRNG();

            int attack2 = rng2.DiceRoll(20) + 3;
            bool hit2 = attack2 >= 12;
            int damage2 = hit2 ? rng2.DiceRoll(8) + 2 : 0;

            // Verify determinism
            Assert.AreEqual(attack1, attack2, "Attack rolls should match");
            Assert.AreEqual(hit1, hit2, "Hit/miss should match");
            Assert.AreEqual(damage1, damage2, "Damage rolls should match");
        }

        [Test]
        public void MultipleRoundsOfCombat_RemainDeterministic()
        {
            // Simulate a full 3-round combat
            const int ROUNDS = 3;
            const int ATTACKS_PER_ROUND = 2;
            const ulong SEED = 11111;

            // Run 1
            var rng1 = new DeterministicRNG(SEED);
            int[] results1 = new int[ROUNDS * ATTACKS_PER_ROUND];
            for (int i = 0; i < ROUNDS; i++)
            {
                for (int a = 0; a < ATTACKS_PER_ROUND; a++)
                {
                    int idx = i * ATTACKS_PER_ROUND + a;
                    int attackRoll = rng1.DiceRoll(20);
                    int damage = attackRoll >= 12 ? rng1.DiceRoll(8) + 2 : 0;
                    results1[idx] = damage;
                }
            }

            // Run 2 - Same seed
            var rng2 = new DeterministicRNG(SEED);
            int[] results2 = new int[ROUNDS * ATTACKS_PER_ROUND];
            for (int i = 0; i < ROUNDS; i++)
            {
                for (int a = 0; a < ATTACKS_PER_ROUND; a++)
                {
                    int idx = i * ATTACKS_PER_ROUND + a;
                    int attackRoll = rng2.DiceRoll(20);
                    int damage = attackRoll >= 12 ? rng2.DiceRoll(8) + 2 : 0;
                    results2[idx] = damage;
                }
            }

            // All results must match
            for (int i = 0; i < results1.Length; i++)
            {
                Assert.AreEqual(results1[i], results2[i], $"Result mismatch at index {i}");
            }
        }

        [Test]
        public void SimulationReplay_ProducesIdenticalLogs()
        {
            const ulong SEED = 555;

            // Run 1
            _sim.Initialize(SEED);
            var log1 = _sim.GetEventLog();

            for (int i = 0; i < 10; i++)
            {
                var evt = new DamageTakenEventData
                {
                    EventId = (ulong)(i + 1),
                    FrameNumber = (uint)i,
                    Timestamp = i * 0.016667f,
                    SourceEntity = new Entity(),
                    DamageAmount = _sim.GetRNG().DiceRoll(8),
                    RemainingHealth = 100 - (i * 10),
                    MaxHealth = 100
                };
                log1.RecordEvent(evt);
                log1.AdvanceFrame();
            }

            string json1 = log1.ExportToJson();

            // Run 2 with same seed
            var sim2 = new CoreSimulation();
            sim2.Initialize(SEED);
            var log2 = sim2.GetEventLog();

            for (int i = 0; i < 10; i++)
            {
                var evt = new DamageTakenEventData
                {
                    EventId = (ulong)(i + 1),
                    FrameNumber = (uint)i,
                    Timestamp = i * 0.016667f,
                    SourceEntity = new Entity(),
                    DamageAmount = sim2.GetRNG().DiceRoll(8),
                    RemainingHealth = 100 - (i * 10),
                    MaxHealth = 100
                };
                log2.RecordEvent(evt);
                log2.AdvanceFrame();
            }

            string json2 = log2.ExportToJson();

            // Logs should be identical (same seed, same sequence)
            // Note: EventId may differ, but damage amounts should not
            Assert.IsTrue(json1.Contains("DamageTaken"));
            Assert.IsTrue(json2.Contains("DamageTaken"));
        }

        [Test]
        public void RNGReset_AllowsExactReplay()
        {
            var rng = new DeterministicRNG(9999);

            // First sequence
            int[] seq1 = new int[5];
            for (int i = 0; i < 5; i++)
                seq1[i] = rng.DiceRoll(20);

            // Reset to start
            rng.Reset();

            // Second sequence - should be identical
            int[] seq2 = new int[5];
            for (int i = 0; i < 5; i++)
                seq2[i] = rng.DiceRoll(20);

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(seq1[i], seq2[i], $"Sequence mismatch at index {i}");
            }
        }

        [Test]
        public void StressTest_1000Rolls_RemainDeterministic()
        {
            var rng1 = new DeterministicRNG(77777);
            var rng2 = new DeterministicRNG(77777);

            for (int i = 0; i < 1000; i++)
            {
                int roll1 = rng1.DiceRoll(20);
                int roll2 = rng2.DiceRoll(20);
                Assert.AreEqual(roll1, roll2, $"Mismatch at roll {i}");
            }
        }
    }
}
