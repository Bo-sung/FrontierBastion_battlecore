using System;
using System.Globalization;

namespace BattleSim.Core.FixedPoint
{
    /// <summary>
    /// Deterministic Q-format fixed-point number.
    /// 1.0 == <see cref="BattleCoreDefaults.FixedPointScale"/> (10000).
    /// Internal representation is a signed 64-bit integer (raw).
    /// At the API/DB boundary, convert via <see cref="Raw"/> / <see cref="FromRaw(long)"/>.
    /// </summary>
    public readonly struct Fp : IEquatable<Fp>, IComparable<Fp>
    {
        public const long Scale = BattleCoreDefaults.FixedPointScale;

        private readonly long _raw;

        private Fp(long raw)
        {
            _raw = raw;
        }

        public long Raw
        {
            get { return _raw; }
        }

        public static Fp Zero
        {
            get { return new Fp(0); }
        }

        public static Fp One
        {
            get { return new Fp(Scale); }
        }

        public static Fp FromRaw(long raw)
        {
            return new Fp(raw);
        }

        public static Fp FromInt(int value)
        {
            return new Fp((long)value * Scale);
        }

        public static Fp FromLong(long value)
        {
            return new Fp(value * Scale);
        }

        /// <summary>
        /// Constructs from numerator/denominator using truncated integer division.
        /// </summary>
        public static Fp FromFraction(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException("Fp.FromFraction denominator is zero.");
            }
            return new Fp(numerator * Scale / denominator);
        }

        public static Fp operator +(Fp a, Fp b)
        {
            return new Fp(a._raw + b._raw);
        }

        public static Fp operator -(Fp a, Fp b)
        {
            return new Fp(a._raw - b._raw);
        }

        public static Fp operator -(Fp a)
        {
            return new Fp(-a._raw);
        }

        /// <summary>
        /// (a.raw * b.raw) / Scale with truncation toward zero.
        /// </summary>
        public static Fp operator *(Fp a, Fp b)
        {
            return new Fp(a._raw * b._raw / Scale);
        }

        /// <summary>
        /// (a.raw * Scale) / b.raw with truncation toward zero.
        /// </summary>
        public static Fp operator /(Fp a, Fp b)
        {
            if (b._raw == 0)
            {
                throw new DivideByZeroException("Fp divide by zero.");
            }
            return new Fp(a._raw * Scale / b._raw);
        }

        public static Fp operator *(Fp a, int b)
        {
            return new Fp(a._raw * b);
        }

        public static Fp operator /(Fp a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Fp divide by zero.");
            }
            return new Fp(a._raw / b);
        }

        public static bool operator ==(Fp a, Fp b)
        {
            return a._raw == b._raw;
        }

        public static bool operator !=(Fp a, Fp b)
        {
            return a._raw != b._raw;
        }

        public static bool operator <(Fp a, Fp b)
        {
            return a._raw < b._raw;
        }

        public static bool operator >(Fp a, Fp b)
        {
            return a._raw > b._raw;
        }

        public static bool operator <=(Fp a, Fp b)
        {
            return a._raw <= b._raw;
        }

        public static bool operator >=(Fp a, Fp b)
        {
            return a._raw >= b._raw;
        }

        public bool Equals(Fp other)
        {
            return _raw == other._raw;
        }

        public override bool Equals(object obj)
        {
            return obj is Fp other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _raw.GetHashCode();
        }

        public int CompareTo(Fp other)
        {
            return _raw.CompareTo(other._raw);
        }

        public override string ToString()
        {
            // Render with up to 4 fractional digits (Scale = 10000).
            long whole = _raw / Scale;
            long frac = _raw % Scale;
            if (frac < 0)
            {
                frac = -frac;
            }
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1:D4}", whole, frac);
        }
    }
}
