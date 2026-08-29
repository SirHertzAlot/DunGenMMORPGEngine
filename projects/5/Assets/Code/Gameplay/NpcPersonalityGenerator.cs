using DunGen.ECS.Core;
using DunGen.Simulation.RNG;
using Unity.Collections;

namespace DunGen.Gameplay
{
    /// <summary>
    /// Deterministically generates an NpcPersonalityComponent from a world seed,
    /// entity index, and archetype string.
    ///
    /// Seeding: seed = worldSeed + entityIndex * 7919  (7919 is prime, spreads values)
    ///
    /// Archetype biases are applied as clamps AFTER generation so individual
    /// variance still exists within each archetype's natural range.
    /// </summary>
    public static class NpcPersonalityGenerator
    {
        // Archetype biases: (minAggression, maxAggression, minCowardice, maxCowardice,
        //                    minGreed, maxGreed, minLoyalty, maxLoyalty,
        //                    minCuriosity, maxCuriosity, minVengefulness, maxVengefulness)
        private static readonly (byte, byte, byte, byte, byte, byte, byte, byte, byte, byte, byte, byte) DefaultBias =
            (20, 80,  10, 70,  10, 70,  10, 70,  10, 70,  10, 70);

        public static NpcPersonalityComponent Generate(int entityIndex, int worldSeed, string archetype)
        {
            var rng = new DeterministicRNG((ulong)(uint)(worldSeed + entityIndex * 7919));

            byte Roll(int min, int max) =>
                (byte)UnityEngine.Mathf.Clamp(min + rng.RollDice(max - min + 1) - 1, 0, 100);

            var (minAgg, maxAgg, minCow, maxCow, minGrd, maxGrd, minLoy, maxLoy, minCur, maxCur, minVen, maxVen)
                = GetArchetypeBias(archetype);

            return new NpcPersonalityComponent
            {
                Aggression   = Roll(minAgg, maxAgg),
                Cowardice    = Roll(minCow, maxCow),
                Greed        = Roll(minGrd, maxGrd),
                Loyalty      = Roll(minLoy, maxLoy),
                Curiosity    = Roll(minCur, maxCur),
                Vengefulness = Roll(minVen, maxVen),
                ArchetypeName = new FixedString32Bytes(archetype ?? "unknown")
            };
        }

        private static (byte, byte, byte, byte, byte, byte, byte, byte, byte, byte, byte, byte)
            GetArchetypeBias(string archetype)
        {
            switch ((archetype ?? "").ToLowerInvariant())
            {
                // Low HP, jumpy — high Cowardice + Greed, moderate Aggression in numbers
                case "goblin":
                    return (30, 70,  50, 90,  60, 100, 10, 40,  20, 60,  20, 60);

                // Mindless, fearless — maximum Aggression, zero Cowardice/Curiosity
                case "skeleton":
                case "zombie":
                case "undead":
                    return (70, 100, 0,  15,  0,  30,  5,  30,  0,  25,  10, 50);

                // Fanatical — high Loyalty + Vengefulness, moderate-high Aggression
                case "cultist":
                case "fanatic":
                    return (50, 90,  10, 40,  10, 50,  60, 100, 15, 50,  55, 100);

                // Tactical — high Curiosity + moderate all-round
                case "bandit":
                case "mercenary":
                    return (40, 80,  20, 60,  40, 80,  20, 60,  30, 70,  30, 70);

                // Bestial — pure aggression, pack loyalty
                case "wolf":
                case "beast":
                    return (60, 100, 10, 40,  5,  30,  50, 90,  20, 50,  15, 55);

                // Intelligent — all traits balanced, Curiosity high
                case "mage":
                case "wizard":
                case "sorcerer":
                    return (30, 70,  20, 60,  20, 60,  20, 60,  60, 100, 30, 70);

                // Guards — high Loyalty, lower Greed
                case "guard":
                case "soldier":
                    return (40, 80,  10, 40,  5,  35,  65, 100, 20, 55,  25, 65);

                default:
                    return DefaultBias;
            }
        }
    }
}
