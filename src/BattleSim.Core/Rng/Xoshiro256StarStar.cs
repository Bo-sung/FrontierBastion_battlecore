using System;

namespace BattleSim.Core.Rng
{
    /// <summary>
    /// Xoshiro256** deterministic PRNG.
    /// Seed expansion uses <see cref="SplitMix64"/> so a single int64 seed
    /// produces all four state words.
    /// Intentionally <c>internal</c>: the simulation RNG is a fixed Core
    /// implementation and is not exposed through a public interface.
    /// Reference: https://prng.di.unimi.it/xoshiro256starstar.c
    /// </summary>
    internal sealed class Xoshiro256StarStar
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public Xoshiro256StarStar(long seed)
        {
            // SplitMix64 is initialized with the unsigned reinterpretation
            // of the int64 seed so that signed-vs-unsigned does not change
            // the resulting stream across platforms.
            SplitMix64 sm = new SplitMix64(unchecked((ulong)seed));
            _s0 = sm.NextUInt64();
            _s1 = sm.NextUInt64();
            _s2 = sm.NextUInt64();
            _s3 = sm.NextUInt64();

            // Xoshiro requires non-zero state. SplitMix64 with the all-zero
            // expansion path is vanishingly unlikely but guard anyway.
            if ((_s0 | _s1 | _s2 | _s3) == 0UL)
            {
                _s0 = 0x9E3779B97F4A7C15UL;
            }
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
                ulong t = _s1 << 17;

                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;

                _s2 ^= t;
                _s3 = RotateLeft(_s3, 45);

                return result;
            }
        }

        public long NextInt64()
        {
            return unchecked((long)NextUInt64());
        }

        /// <summary>
        /// Uniform integer in [minInclusive, maxExclusive). Uses rejection
        /// sampling so the result is unbiased.
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentException("maxExclusive must be greater than minInclusive.");
            }
            uint range = unchecked((uint)(maxExclusive - minInclusive));
            uint limit = uint.MaxValue - (uint.MaxValue % range);
            while (true)
            {
                uint sample = unchecked((uint)(NextUInt64() >> 32));
                if (sample < limit)
                {
                    return minInclusive + (int)(sample % range);
                }
            }
        }

        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }
    }
}
