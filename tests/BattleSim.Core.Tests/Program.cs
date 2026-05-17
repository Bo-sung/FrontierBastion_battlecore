using System;
using BattleSim.Core;

AssertEqual(20, BattleCoreDefaults.TickRate, nameof(BattleCoreDefaults.TickRate));
AssertEqual(50, BattleCoreDefaults.TickMilliseconds, nameof(BattleCoreDefaults.TickMilliseconds));
AssertEqual(10000, BattleCoreDefaults.FixedPointScale, nameof(BattleCoreDefaults.FixedPointScale));

Console.WriteLine("BattleSim.Core smoke checks passed.");

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!object.Equals(expected, actual))
    {
        throw new InvalidOperationException(string.Format("{0}: expected {1}, actual {2}", name, expected, actual));
    }
}
