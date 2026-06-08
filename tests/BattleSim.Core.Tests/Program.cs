using System;
using BattleSim.Core;
using BattleSim.Core.Tests.Determinism;
using BattleSim.Core.Tests.FixedPoint;
using BattleSim.Core.Tests.Simulation;

// Mode: print RNG golden values for copy-paste into RngTests.cs.
if (args.Length >= 1 && args[0] == "--print-rng-golden")
{
    long seed = 42L;
    int count = 8;
    if (args.Length >= 2) seed = long.Parse(args[1]);
    if (args.Length >= 3) count = int.Parse(args[2]);
    RngTests.PrintGolden(seed, count);
    return;
}

AssertEqual(16, BattleCoreDefaults.TickRate, nameof(BattleCoreDefaults.TickRate));
AssertEqual(62.5m, BattleCoreDefaults.TickMilliseconds, nameof(BattleCoreDefaults.TickMilliseconds));
AssertEqual(10000, BattleCoreDefaults.FixedPointScale, nameof(BattleCoreDefaults.FixedPointScale));

FpTests.Run();
Console.WriteLine("Fp tests passed.");

RngTests.Run();
Console.WriteLine("RNG tests passed.");

SimulatorTests.Run();
Console.WriteLine("Simulator + smoke tests passed.");

Console.WriteLine("BattleSim.Core all checks passed.");

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!object.Equals(expected, actual))
    {
        throw new InvalidOperationException(string.Format("{0}: expected {1}, actual {2}", name, expected, actual));
    }
}
