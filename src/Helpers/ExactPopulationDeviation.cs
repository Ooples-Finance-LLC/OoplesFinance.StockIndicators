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
