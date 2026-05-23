namespace BattleSim.Core
{
    public static class BattleCoreDefaults
    {
        public const int TickRate = 20;
        public const int TickMilliseconds = 50;
        public const int FixedPointScale = 10000;
        public const long MinDamageRaw = FixedPointScale;
        public const long ProjectileHitRadiusMilli = 100;
        public const int ProjectileDefaultTtlTick = 200;
    }
}
