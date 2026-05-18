namespace BattleSim.Core.Rng
{
    /// <summary>
    /// SplitMix64 — used only to expand a single 64-bit seed into the
    /// four 64-bit state words required by Xoshiro256**.
    /// Not exposed as the simulation RNG.
    /// </summary>
    internal struct SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
