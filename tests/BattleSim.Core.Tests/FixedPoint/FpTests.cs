using System;
using BattleSim.Core.FixedPoint;

namespace BattleSim.Core.Tests.FixedPoint
{
    internal static class FpTests
    {
        public static void Run()
        {
            ConstantsRoundTrip();
            AdditionAndSubtraction();
            MultiplicationTruncates();
            DivisionTruncates();
            FromFractionTruncates();
            Comparison();
            Negation();
            DivideByZeroThrows();
            RawRoundTrip();
        }

        private static void ConstantsRoundTrip()
        {
            AssertEqual(0L, Fp.Zero.Raw, "Fp.Zero.Raw");
            AssertEqual(10000L, Fp.One.Raw, "Fp.One.Raw");
            AssertEqual(10000L, Fp.FromInt(1).Raw, "Fp.FromInt(1).Raw");
            AssertEqual(-30000L, Fp.FromInt(-3).Raw, "Fp.FromInt(-3).Raw");
        }

        private static void AdditionAndSubtraction()
        {
            Fp a = Fp.FromInt(2);
            Fp b = Fp.FromInt(3);
            AssertEqual(50000L, (a + b).Raw, "2+3");
            AssertEqual(-10000L, (a - b).Raw, "2-3");
        }

        private static void MultiplicationTruncates()
        {
            // 1.5 * 2.5 = 3.75
            Fp a = Fp.FromFraction(3, 2);  // 1.5
            Fp b = Fp.FromFraction(5, 2);  // 2.5
            AssertEqual(37500L, (a * b).Raw, "1.5 * 2.5");

            // Truncation toward zero: 0.0001 * 0.0001 == 0 at scale 10000.
            Fp tiny = Fp.FromRaw(1);
            AssertEqual(0L, (tiny * tiny).Raw, "tiny*tiny truncates");
        }

        private static void DivisionTruncates()
        {
            // 1 / 3 at scale 10000 = 3333 (truncated).
            Fp one = Fp.One;
            Fp three = Fp.FromInt(3);
            AssertEqual(3333L, (one / three).Raw, "1/3 truncates to 3333");
        }

        private static void FromFractionTruncates()
        {
            // 10 / 3 = 3.333..., raw = 33333.
            AssertEqual(33333L, Fp.FromFraction(10, 3).Raw, "FromFraction 10/3");
        }

        private static void Comparison()
        {
            Fp a = Fp.FromInt(1);
            Fp b = Fp.FromInt(2);
            AssertTrue(a < b, "1 < 2");
            AssertTrue(b > a, "2 > 1");
            AssertTrue(a <= Fp.FromInt(1), "1 <= 1");
            AssertTrue(a == Fp.FromInt(1), "1 == 1");
            AssertTrue(a != b, "1 != 2");
            AssertEqual(0, a.CompareTo(Fp.FromInt(1)), "CompareTo equal");
        }

        private static void Negation()
        {
            Fp a = Fp.FromInt(5);
            AssertEqual(-50000L, (-a).Raw, "-5");
        }

        private static void DivideByZeroThrows()
        {
            bool threw = false;
            try { var _ = Fp.One / Fp.Zero; }
            catch (DivideByZeroException) { threw = true; }
            AssertTrue(threw, "Fp / 0 throws");
        }

        private static void RawRoundTrip()
        {
            long raw = 1234567L;
            AssertEqual(raw, Fp.FromRaw(raw).Raw, "Raw round-trip");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + ": expected " + expected + ", actual " + actual);
            }
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name + ": expected true");
            }
        }
    }
}
