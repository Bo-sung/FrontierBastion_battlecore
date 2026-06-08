namespace BattleSim.Core
{
    public static class BattleCoreDefaults
    {
        public const int TickRate = 16;
        public const decimal TickMilliseconds = 62.5m;
        public const int TickMillisecondsNumerator = 125;
        public const int TickMillisecondsDenominator = 2;
        public const int FixedPointScale = 10000;
        public const long MinDamageRaw = FixedPointScale;
        public const long ProjectileHitRadiusMilli = 100;
        public const int ProjectileDefaultTtlTick = 200;
        public const long KnockbackDistanceMilli = 200;
    }
}
