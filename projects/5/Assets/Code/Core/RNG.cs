using System;
using UnityEngine;

namespace DunGen.Simulation.RNG
{
    /// <summary>
    /// Deterministic, seed-based random number generator.
    /// Uses Linear Congruential Generator (LCG) for predictable, reproducible randomness.
    /// Same seed always produces the same sequence of numbers.
    /// </summary>
    public class DeterministicRNG
    {
        private const ulong A = 6364136223846793005UL;
        private const ulong C = 1442695040888963407UL;
        
        private ulong _state;
        private ulong _seed;

        public DeterministicRNG() : this(0UL)
        {
        }

        public DeterministicRNG(ulong seed)
        {
            _seed = seed;
            _state = seed;
        }

        public DeterministicRNG(int seed) : this((ulong)seed)
        {
        }

        /// <summary>
        /// Returns next random value in [0, 1) range (float).
        /// </summary>
        public float NextFloat()
        {
            _state = A * _state + C;
            // Convert ulong to float in [0, 1)
            return (_state >> 11) * (1.0f / 9007199254740992.0f);
        }

        /// <summary>
        /// Returns next random integer in [0, maxExclusive) range.
        /// </summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentException("maxExclusive must be positive");
            
            return (int)(NextFloat() * maxExclusive);
        }

        /// <summary>
        /// Returns next random integer in [minInclusive, maxExclusive) range.
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException("maxExclusive must be greater than minInclusive");
            
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>
        /// Rolls a die with specified number of sides.
        /// D20 roll: DiceRoll(20)
        /// D6 roll: DiceRoll(6)
        /// </summary>
        public int DiceRoll(int sides)
        {
            return 1 + NextInt(sides);
        }

        public int RollDice(int sides) => DiceRoll(sides);

        public int RollD20() => DiceRoll(20);

        /// <summary>
        /// Rolls multiple dice and returns sum (e.g., 2d6, 3d4+5).
        /// </summary>
        public int DiceRollMultiple(int count, int sides)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
                sum += DiceRoll(sides);
            return sum;
        }

        /// <summary>
        /// Reset RNG to original seed state.
        /// </summary>
        public void Reset()
        {
            _state = _seed;
        }

        public void SetSeed(ulong seed)
        {
            _seed = seed;
            _state = seed;
        }

        public void SetSeed(uint seed)
        {
            SetSeed((ulong)seed);
        }

        /// <summary>
        /// Get current seed for this RNG instance.
        /// </summary>
        public ulong GetSeed() => _seed;

        public ulong Seed => _seed;

        /// <summary>
        /// Get current internal state (useful for debugging/logging).
        /// </summary>
        public ulong GetState() => _state;
    }
}
