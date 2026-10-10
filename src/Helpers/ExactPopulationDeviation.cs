using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exceptional-range fallback. Inputs are integers in units of 2^-1074.
// N*sum(x*x)-sum(x)^2 is nonnegative and exact; no floating squares are formed.
internal struct ExactPopulationDeviation
{
    private BigInteger _sum, _squares;
    private int _count;

    internal void Add(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var integer = new BigInteger(bits & ((1L << 52) - 1));
        if (exponent != 0) integer = (integer + (BigInteger.One << 52)) << (exponent - 1);
        if (bits < 0) integer = -integer;
        _sum += integer;
        _squares += integer * integer;
        _count++;
    }

    internal double Value()
    {
        if (_count == 0) return 0;
        var radicand = _count * _squares - _sum * _sum;
        if (radicand.IsZero) return 0;
        var root = IntegerRoot(radicand);
        var whole = root / _count;
        var shift = Math.Max(0, BitLength(whole) - 53);
        var divisor = new BigInteger(_count) << shift;
        var significand = root / divisor;
        var midpoint = divisor * (2 * significand + 1);
        var comparison = (4 * radicand).CompareTo(midpoint * midpoint);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        var power = shift - 1074;
        var scale = power >= -1022
            ? BitConverter.Int64BitsToDouble((long)(power + 1023) << 52)
            : BitConverter.Int64BitsToDouble(1L << shift);
        return (double)significand * scale;
    }

    // Correctly round sqrt(numerator / denominator) in units of 2^-1074.
    // Unlike rounding the variance first, this preserves subnormal deviations.
    internal static double RootRatio(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign < 0 || denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(numerator));
        if (numerator.IsZero) return 0;
        var exponent = BitLength(numerator) - BitLength(denominator);
        if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
        var shift = Math.Max(0, exponent / 2 - 52);
        // Normalize before taking the integer root: at most 106 quotient bits,
        // even when the original moments occupy thousands of binary places.
        var scaledDenominator = denominator << (2 * shift);
        var quotient = numerator / scaledDenominator;
        var significand = quotient.IsZero ? BigInteger.Zero : IntegerRoot(quotient);
        var midpoint = 2 * significand + 1;
        var comparison = (4 * numerator).CompareTo(scaledDenominator * midpoint * midpoint);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        return ExactMeanAccumulator.Encode((ulong)significand, shift, false);
    }

    // Correctly round sqrt(numerator / denominator * 2^binaryExponent).
    // Shift only to the final rounding grid, not to the subnormal grid first.
    internal static double ScaledRootRatio(BigInteger numerator, BigInteger denominator, int binaryExponent)
    {
        if (numerator.Sign < 0 || denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(numerator));
        if (numerator.IsZero) return 0;
#if !NETFRAMEWORK
        if (numerator <= (1UL << 53) && denominator <= 4096
            && TrySmallScaledRoot((ulong)numerator, (uint)denominator, binaryExponent, out var small)) return small;
#endif
        var exponent = BitLength(numerator) - BitLength(denominator);
        if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
        exponent += binaryExponent;
        var rootExponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
        var grid = Math.Max(-1074, rootExponent - 52);
        // Normalize before taking the integer root: at most 106 quotient bits,
        // even when the original moments occupy thousands of binary places.
        var shift = binaryExponent - 2 * grid;
        if (shift >= 0) numerator <<= shift;
        else denominator <<= -shift;
        var quotient = numerator / denominator;
        var significand = quotient.IsZero ? BigInteger.Zero : IntegerRoot(quotient);
        var midpoint = 2 * significand + 1;
        var comparison = (4 * numerator).CompareTo(denominator * midpoint * midpoint);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        return ExactMeanAccumulator.Encode((ulong)significand, grid + 1074, false);
    }

#if !NETFRAMEWORK
    // The floating estimate is never trusted for rounding. Compare the exact
    // rational with both binary64 midpoints using bounded 128-bit integers.
    // Wider operands and subnormal/overflow results keep the general path.
    internal static bool TrySmallScaledRoot(ulong numerator, uint denominator, int power, out double result)
    {
        result = 0;
        if (numerator == 0 || numerator > (1UL << 53) || denominator is 0 or > 4096) return false;
        if ((power & 1) != 0)
        {
            if (numerator > (1UL << 52)) return false;
            numerator <<= 1;
            power--;
        }
        var bits = BitConverter.DoubleToInt64Bits(Math.Sqrt((double)numerator / denominator));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var exponent = (int)(bits >> 52) - 1023;
            var significand = (ulong)(bits & 0xfffffffffffffL) | (1UL << 52);
            // At a power of two, the preceding spacing is half the following spacing.
            var boundary = significand == (1UL << 52);
            var lower = boundary ? (significand << 2) - 1 : (significand << 1) - 1;
            var lowerPower = exponent - (boundary ? 54 : 53);
            if (!CompareMidpoint(numerator, denominator, lower, lowerPower, out var comparison)) return false;
            if (comparison < 0 || comparison == 0 && (bits & 1) != 0) { bits--; continue; }
            if (!CompareMidpoint(numerator, denominator, (significand << 1) + 1, exponent - 53, out comparison)) return false;
            if (comparison > 0 || comparison == 0 && (bits & 1) != 0) { bits++; continue; }
            var scaledExponent = (long)exponent + power / 2;
            if (scaledExponent is < -1022 or > 1023) return false;
            result = BitConverter.Int64BitsToDouble(bits + ((long)(power / 2) << 52));
            return true;
        }
        return false;
    }

    private static bool CompareMidpoint(ulong numerator, uint denominator, ulong midpoint, int power, out int comparison)
    {
        comparison = 0;
        var shift = -2 * power;
        if (shift is < 0 or >= 128 || (UInt128)numerator > (UInt128.MaxValue >> shift)) return false;
        // midpoint < 2^55 and denominator <= 2^12, hence the product fits 122 bits.
        var left = (UInt128)numerator << shift;
        var right = (UInt128)midpoint * midpoint * denominator;
        comparison = left.CompareTo(right);
        return true;
    }
#endif

    internal static BigInteger IntegerRoot(BigInteger value)
    {
        var current = BigInteger.One << ((BitLength(value) + 1) / 2);
        while (true)
        {
            var next = (current + value / current) >> 1;
            if (next >= current) return current;
            current = next;
        }
    }

    private static int BitLength(BigInteger value)
    {
        var bytes = value.ToByteArray();
        var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var bits = last * 8;
        for (var head = bytes[last]; head != 0; head >>= 1) bits++;
        return bits;
    }
}
