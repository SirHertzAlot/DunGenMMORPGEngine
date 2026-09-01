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
    /// Edge-case coverage for the deterministic RNG (S1-5 first unit test).
    /// Complements the existing golden-sequence locks: these verify boundary
    /// behavior (invalid args throw, dice stay in range, reset restores parity)
    /// that the golden tests do not cover. Runs under xUnit (backend) and the
    /// Unity test assembly via the dual-framework guard.
    /// </summary>
    public class DeterministicRngEdgeCaseTests
    {
        [FactAttribute]
        public void NextInt_MaxExclusiveNonPositive_Throws()
        {
            var rng = new DeterministicRng(1UL);
            Assert.Throws<ArgumentException>(() => rng.NextInt(0));
            Assert.Throws<ArgumentException>(() => rng.NextInt(-5));
        }

        [FactAttribute]
        public void NextInt_MinGteMax_Throws()
        {
            var rng = new DeterministicRng(1UL);
            Assert.Throws<ArgumentException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentException>(() => rng.NextInt(10, 4));
        }

        [FactAttribute]
        public void DiceRoll_StaysWithinSides_ForSeed42()
        {
            var rng = new DeterministicRng(42UL);
            for (int i = 0; i < 1000; i++)
            {
                int roll = rng.DiceRoll(20);
                Assert.InRange(roll, 1, 20);
            }
        }

        [FactAttribute]
        public void Reset_RestoresIdenticalSequence()
        {
            var rng = new DeterministicRng(42UL);
            int[] first = { rng.DiceRoll(20), rng.DiceRoll(20), rng.DiceRoll(20) };
            rng.NextFloat(); // advance state away from seed
            rng.NextInt(1000);
            rng.Reset();
            int[] second = { rng.DiceRoll(20), rng.DiceRoll(20), rng.DiceRoll(20) };
            Assert.Equal(first, second);
        }

        [FactAttribute]
        public void SetSeed_NewSeedReproducesItsOwnSequence()
        {
            var a = new DeterministicRng(7UL);
            var b = new DeterministicRng(7UL);
            int[] seqA = { a.NextInt(100), a.NextInt(100), a.NextInt(100) };
            int[] seqB = { b.NextInt(100), b.NextInt(100), b.NextInt(100) };
            Assert.Equal(seqA, seqB);
        }
    }
}
#endif