using System;
using Authoritative.Multiplayer;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Tests
{
    /// <summary>
    /// Locks the DeterministicRng to the byte-for-byte portable mirror of the
    /// Unity-side RNG. Any change to the LCG or its draw semantics breaks these
    /// golden sequences and is caught here.
    /// </summary>
    public class DeterministicRngTests
    {
        [FactAttribute]
        public void Seed42_D20Sequence_MatchesLockedGoldenValues()
        {
            var rng = new DeterministicRng(42UL);
            Assert.Equal(12, rng.RollD20());
            Assert.Equal(5, rng.RollD20());
            Assert.Equal(9, rng.RollD20());
            Assert.Equal(13, rng.RollD20());
            Assert.Equal(14, rng.RollD20());
            Assert.Equal(1, rng.RollD20());
            Assert.Equal(1, rng.RollD20());
            Assert.Equal(4, rng.RollD20());
        }

        [FactAttribute]
        public void Seed42_MinMaxOverload_MatchesLockedGoldenValues()
        {
            var rng = new DeterministicRng(42UL);
            Assert.Equal(12, rng.Next(6, 18));
            Assert.Equal(8, rng.Next(6, 18));
            Assert.Equal(10, rng.Next(6, 18));
            Assert.Equal(13, rng.Next(6, 18));
            Assert.Equal(14, rng.Next(6, 18));
            Assert.Equal(6, rng.Next(6, 18));
        }

        [FactAttribute]
        public void Seed42_MaxOnlyOverload_MatchesLockedGoldenValues()
        {
            var rng = new DeterministicRng(42UL);
            Assert.Equal(2, rng.Next(5));
            Assert.Equal(1, rng.Next(5));
            Assert.Equal(2, rng.Next(5));
            Assert.Equal(3, rng.Next(5));
            Assert.Equal(3, rng.Next(5));
        }

        [FactAttribute]
        public void Seed7_MultipleDice_MatchesLockedGoldenValues()
        {
            var rng = new DeterministicRng(7UL);
            Assert.Equal(15, rng.DiceRollMultiple(3, 6));
            Assert.Equal(5, rng.DiceRollMultiple(3, 6));
        }

        [FactAttribute]
        public void SameSeed_TwoInstances_ProduceIdenticalIndependentSequences()
        {
            var first = new DeterministicRng(2026UL);
            var second = new DeterministicRng(2026UL);

            for (int i = 0; i < 25; i++)
            {
                // Identical operations on both instances; states stay in lockstep.
                Assert.Equal(first.RollD20(), second.RollD20());
                Assert.Equal(first.Next(1, 100), second.Next(1, 100));
                Assert.Equal(first.RollDice(8), second.RollDice(8));
                Assert.Equal(first.Next(), second.Next());
            }

            Assert.Equal(first.GetState(), second.GetState());
        }

        [FactAttribute]
        public void Reset_RestoresTheOriginalSeedState()
        {
            var rng = new DeterministicRng(1234UL);
            rng.RollD20();
            rng.Next(100);
            rng.Reset();
            Assert.Equal(1234UL, rng.GetState());
        }
    }
}
#endif