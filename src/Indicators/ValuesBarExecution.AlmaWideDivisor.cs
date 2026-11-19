#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Private ALMA publication arithmetic. The denominator stays fixed between
    // blocks; ordinary word denominators retain the existing Jurik publisher.
    internal readonly struct AlmaWideDivisor
    {
        private readonly UInt128 _denominator;
        private readonly ulong _reciprocal;
        private readonly int _bits;

        internal AlmaWideDivisor(UInt128 denominator)
        {
            if (denominator <= ulong.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            _denominator = denominator;
            _reciprocal = (ulong)(UInt128.MaxValue / denominator);
            _bits = Bits(denominator);
        }

        internal (UInt128 Quotient, UInt128 Remainder) Divide(UInt128 numerator)
        {
            if (_denominator == 0) throw new DivideByZeroException();
            ulong low = unchecked((ulong)numerator), high = (ulong)(numerator >> 64);
            ulong lowProductHigh = Math.BigMul(low, _reciprocal, out _);
            UInt128 upper = (UInt128)high * _reciprocal + lowProductHigh;
            ulong estimate = (ulong)(upper >> 64);
            UInt128 remainder = numerator - (UInt128)estimate * _denominator;
            // M=floor((2^128-1)/D) underestimates 2^128/D by at most one.
            // n<2^128 proves q0<=floor(n/D)<=q0+1, including power-of-two D.
            // Hence q0*D<=n and one exact correction suffices without overflow.
            if (remainder >= _denominator)
            {
                estimate++;
                remainder -= _denominator;
            }
            return (estimate, remainder);
        }

        internal bool TryRatio(Int128 numerator, int scale, out double result)
        {
            result = 0;
            if (_denominator == 0) return false;
            if (numerator == 0) return true; // Preserve canonical exact zero.
            bool negative = numerator < 0;
            UInt128 top = negative ? (UInt128)(-(numerator + 1)) + 1 : (UInt128)numerator;
            int topBits = Bits(top), power = topBits - _bits;
            if (power >= 0 ? top < (_denominator << power) : (top << -power) < _denominator) power--;
            long exponent = (long)power + scale;
            if (exponent > 1023) { result = negative ? double.NegativeInfinity : double.PositiveInfinity; return true; }
            if (exponent < -1075) { result = negative ? -0d : 0d; return true; }
            int grid = Math.Max((int)exponent - 52, -1074);
            long shift = (long)scale - grid;
            // Retain the original scale-aware fallback when rounding would
            // require a shifted denominator or more than a UInt128 numerator.
            if (shift is < 0 or >= 128 || topBits + shift > 128) return false;
            var (quotient, remainder) = Divide(top << (int)shift);
            var complement = _denominator - remainder;
            if (remainder > complement || remainder == complement && (quotient & 1) != 0) quotient++;
            // The rounding-grid quotient is <=2^53 and converts exactly. The
            // subnormal grid is already applied: never round a ratio then scale.
            result = Math.ScaleB((double)quotient, grid);
            if (negative) result = -result;
            return true;
        }

        private static int Bits(UInt128 value)
        {
            ulong high = (ulong)(value >> 64);
            return high == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)value)
                : 128 - BitOperations.LeadingZeroCount(high);
        }
    }
}
#endif
