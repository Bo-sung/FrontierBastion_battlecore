using System;
using BattleSim.Core.Rng;

namespace BattleSim.Core.Tests.Determinism
{
    /// <summary>
    /// Golden-sequence regression tests for the internal Xoshiro256**
    /// PRNG (SplitMix64-expanded seed).
    /// Run with argument <c>--print-rng-golden</c> to regenerate the
    /// expected values below.
    /// </summary>
    internal static class RngTests
    {
        // Golden sequence: first 8 ulong outputs for seed = 42.
        // Generated from this same implementation; serves as a regression
        // pin so any future change to the SplitMix64 → Xoshiro256** path
        // is caught.
        private static readonly ulong[] GoldenSeed42 = new ulong[]
        {
            0x15780B2E0C2EC716UL,
            0x6104D9866D113A7EUL,
            0xAE17533239E499A1UL,
            0xECB8AD4703B360A1UL,
            0xFDE6DC7FE2EC5E64UL,
            0xC50DA53101795238UL,
            0xB82154855A65DDB2UL,
            0xD99A2743EBE60087UL,
        };

        public static void Run()
        {
            GoldenSequenceSeed42();
            SameSeedSameStream();
            DifferentSeedsDifferentStreams();
            NextIntInRange();
        }

        public static void PrintGolden(long seed, int count)
        {
            Xoshiro256StarStar rng = new Xoshiro256StarStar(seed);
            Console.WriteLine("Xoshiro256** golden (seed=" + seed + "):");
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("  0x" + rng.NextUInt64().ToString("X16") + "UL,");
            }
        }

        private static void GoldenSequenceSeed42()
        {
            Xoshiro256StarStar rng = new Xoshiro256StarStar(42L);
            for (int i = 0; i < GoldenSeed42.Length; i++)
            {
                ulong actual = rng.NextUInt64();
                if (actual != GoldenSeed42[i])
                {
                    throw new InvalidOperationException(
                        "RNG golden mismatch at index " + i +
                        ": expected 0x" + GoldenSeed42[i].ToString("X16") +
                        ", actual 0x" + actual.ToString("X16"));
                }
            }
        }

        private static void SameSeedSameStream()
        {
            Xoshiro256StarStar a = new Xoshiro256StarStar(12345L);
            Xoshiro256StarStar b = new Xoshiro256StarStar(12345L);
            for (int i = 0; i < 32; i++)
            {
                if (a.NextUInt64() != b.NextUInt64())
                {
                    throw new InvalidOperationException("Same seed produced divergent streams.");
                }
            }
        }

        private static void DifferentSeedsDifferentStreams()
        {
            Xoshiro256StarStar a = new Xoshiro256StarStar(1L);
            Xoshiro256StarStar b = new Xoshiro256StarStar(2L);
            bool anyDifference = false;
            for (int i = 0; i < 8; i++)
            {
                if (a.NextUInt64() != b.NextUInt64())
                {
                    anyDifference = true;
                    break;
                }
            }
            if (!anyDifference)
            {
                throw new InvalidOperationException("Different seeds produced identical first 8 outputs.");
            }
        }

        private static void NextIntInRange()
        {
            Xoshiro256StarStar rng = new Xoshiro256StarStar(7L);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(10, 20);
                if (v < 10 || v >= 20)
                {
                    throw new InvalidOperationException("NextInt out of range: " + v);
                }
            }
        }
    }
}
