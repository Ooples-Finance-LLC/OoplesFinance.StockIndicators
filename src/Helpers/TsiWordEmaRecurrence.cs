#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Only the four primary TSI stages opt in. Their rounded state has canonical
// zero; unsupported inputs retain the original extended-exponent recurrence.
internal readonly struct TsiWordEmaRecurrence
{
    private const ulong FractionMask = (1UL << 52) - 1;
    private readonly int _period;
    private readonly ulong _reciprocal;
    private readonly CompactMacdValue.EmaRecurrence _fallback;

    internal TsiWordEmaRecurrence(int period)
    {
        _period = period;
        _reciprocal = period is >= 1 and <= 2047
            ? (ulong)(((UInt128)1 << 64) / (ulong)(period + 1)) : 0;
        _fallback = new(period);
    }

    internal CompactMacdValue Advance(in CompactMacdValue previous, in CompactMacdValue input)
    {
        if (_reciprocal != 0)
        {
            double prior = previous.Publish(), next = input.Publish();
            // Preserve the existing inexpensive same-sign, same-binade path.
            if (!SameNormalBinade(prior, next) && TryAdvance(prior, next, out double rounded))
                return CompactMacdValue.Of(rounded);
        }
        return _fallback.Advance(previous, input);
    }

    internal bool TryAdvance(double previous, double input, out double rounded)
    {
        rounded = 0;
        if (_reciprocal == 0 || !Parts(previous, out long a, out int ae)
            || !Parts(input, out long b, out int be)) return false;
        if (a == 0) ae = be;
        if (b == 0) be = ae;
        int common = Math.Min(ae, be), leftShift = ae - common, rightShift = be - common;
        if (leftShift > 62 || rightShift > 62) return false;
        // Each weighted shifted term has fewer than 126 magnitude bits under
        // the period/significand/shift guards; their signed sum fits Int128.
        Int128 numerator = ((Int128)a << leftShift) * (_period - 1)
            + 2 * ((Int128)b << rightShift);
        if (numerator == 0) return true;
        bool negative = numerator < 0;
        UInt128 magnitude = (UInt128)(negative ? -numerator : numerator);
        if (magnitude > ulong.MaxValue) return false;
        ulong top = (ulong)magnitude, divisor = (ulong)(_period + 1);
        int topBits = 64 - BitOperations.LeadingZeroCount(top);
        int power = topBits - (64 - BitOperations.LeadingZeroCount(divisor));
        if (power >= 0 ? top < (divisor << power) : (top << -power) < divisor) power--;
        int exponent = common + power;
        if (exponent is < -1022 or > 1023) return false;
        int shift = power - 52;
        if (shift < 0 && topBits - shift > 64) return false;
        ulong scaled = shift < 0 ? top << -shift : top >> shift;
        // floor(2^64/divisor) gives an underestimate of the word quotient by
        // at most one. The exact remainder corrects it before final rounding.
        ulong quotient = (ulong)(((UInt128)scaled * _reciprocal) >> 64);
        ulong multiple = quotient * divisor;
        if (multiple > scaled) return false;
        ulong remainder = scaled - multiple;
        if (remainder >= divisor) { quotient++; remainder -= divisor; }
        if (shift > 0)
        {
            remainder = (remainder << shift) | (top & ((1UL << shift) - 1));
            divisor <<= shift;
        }
        // Restored low bits and the divisor occupy at most 21 bits. No
        // information was discarded, including opposite-sign cancellation.
        int comparison = (remainder << 1).CompareTo(divisor);
        if (comparison > 0 || comparison == 0 && (quotient & 1) != 0) quotient++;
        if (quotient == (1UL << 53)) { quotient >>= 1; exponent++; }
        if (exponent > 1023 || quotient < (1UL << 52) || quotient >= (1UL << 53)) return false;
        ulong bits = ((ulong)(exponent + 1023) << 52) | (quotient & FractionMask);
        if (negative) bits |= 1UL << 63;
        rounded = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        return true;
    }

    private static bool SameNormalBinade(double left, double right)
    {
        ulong a = unchecked((ulong)BitConverter.DoubleToInt64Bits(left));
        ulong b = unchecked((ulong)BitConverter.DoubleToInt64Bits(right));
        ulong exponent = a & 0x7ff0000000000000UL;
        return exponent is not 0 and not 0x7ff0000000000000UL
            && ((a ^ b) & 0xfff0000000000000UL) == 0;
    }

    private static bool Parts(double value, out long mantissa, out int exponent)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        int encoded = (int)((bits >> 52) & 2047);
        ulong fraction = bits & FractionMask;
        mantissa = 0; exponent = 0;
        if (encoded == 0) return fraction == 0;
        if (encoded == 2047) return false;
        mantissa = (long)(fraction | (1UL << 52));
        if ((bits >> 63) != 0) mantissa = -mantissa;
        exponent = encoded - 1075;
        return true;
    }
}
#endif
