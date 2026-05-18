namespace BattleSim.Core.FixedPoint
{
    /// <summary>
    /// Position and distance unit policy.
    /// Values are stored as <see cref="long"/> in milliunits where
    /// <c>1 unit == 1000 milliunits</c>. This is intentionally distinct
    /// from <see cref="Fp"/> (fp10000) to avoid silent unit confusion
    /// between physics quantities and combat scalars.
    /// At the API/DB boundary, milliunits travel as raw int64.
    /// </summary>
    public static class Milliunits
    {
        public const long PerUnit = 1000L;

        public static long FromUnits(int units)
        {
            return (long)units * PerUnit;
        }

        public static long FromUnits(long units)
        {
            return units * PerUnit;
        }
    }
}
