using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Directed fixed-point atanh intervals certify the rounded logarithm before
// dispersion. Ordinary log approximations can erase or invent tiny variance.
internal static partial class CertifiedLogReturn
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        int,
        (BigInteger Low, BigInteger High)
    > LogTwo = new();

    private static BigInteger Ceiling(BigInteger n, BigInteger d) => (n + d - 1) / d;

    private static BigInteger CeilingShift(BigInteger value, int bits)
    {
        if (value.IsZero) return BigInteger.Zero;
        var floor = value >> bits;
        return ExactMeanAccumulator.TrailingBinaryZeros(value) >= bits ? floor : floor + 1;
    }

    private static (BigInteger Low, BigInteger High) Series(BigInteger n, BigInteger d, int bits)
    {
        if (n.IsZero)
            return (0, 0);
        var scale = BigInteger.One << bits;
        var low = BigInteger.DivRem(n << bits, d, out var remainder);
        var high = remainder.IsZero ? low : low + 1;
        // These nonnegative fixed-point divisions have power-of-two divisors.
        // Shifts retain the same directed bounds without general division.
        var squareLow = (low * low) >> bits;
        var squareHigh = CeilingShift(high * high, bits);
        var powerLow = low;
        var powerHigh = high;
        BigInteger sumLow = 0,
            sumHigh = 0;
        var terms = bits / 3 + 8;
        for (var j = 0; j < terms; j++)
        {
            var odd = 2 * j + 1;
            sumLow += powerLow / odd;
            sumHigh += Ceiling(powerHigh, odd);
            powerLow = (powerLow * squareLow) >> bits;
            powerHigh = CeilingShift(powerHigh * squareHigh, bits);
            // Once the next power is at most one fixed-point unit, further
            // rounded terms add no useful precision. The same geometric tail
            // bound remains valid at this earlier truncation point.
            if (powerHigh <= BigInteger.One) { terms = j + 1; break; }
        }
        var tail = Ceiling(powerHigh * scale, (2 * terms + 1) * (scale - squareHigh));
        return (2 * sumLow, 2 * (sumHigh + tail));
    }

    internal static double Of(double current, double previous)
    {
        var currentFactor = new ExactMeanAccumulator.ProductFactor(Math.Abs(current));
        var previousFactor = new ExactMeanAccumulator.ProductFactor(Math.Abs(previous));
#if !NETFRAMEWORK
        if (TryFixed(currentFactor, previousFactor, out var certified)) return certified;
#endif
        var n = new BigInteger(currentFactor.Mantissa);
        var d = new BigInteger(previousFactor.Mantissa);
        int exponent = currentFactor.Scale - previousFactor.Scale;
        if (n == d && exponent == 0) return 0;
        // Normalize only the mantissas. Their common minimum-subnormal padding
        // cancels, and the binary scale difference contributes exponent*log(2).
        int adjustment = (int)(FrameworkCompatibility.GetBitLength(n) - FrameworkCompatibility.GetBitLength(d));
        if (adjustment >= 0 ? n < (d << adjustment) : (n << -adjustment) < d) adjustment--;
        if (adjustment < 0) n <<= -adjustment;
        else d <<= adjustment;
        exponent += adjustment;
        // Move [1, 2) to [3/4, 3/2). The atanh argument then has magnitude
        // at most 1/5, and small decreases no longer cancel a large log(2).
        if (2 * n >= 3 * d) { d <<= 1; exponent++; }
        var difference = n - d;
        var denominator = n + d;
        var negative = difference.Sign < 0;
        difference = BigInteger.Abs(difference);
        for (var bits = 96; ; bits *= 2)
        {
            var core = Series(difference, denominator, bits);
            var lower = negative ? -core.High : core.Low;
            var upper = negative ? -core.Low : core.High;
            if (exponent != 0)
            {
                var two = LogTwo.GetOrAdd(bits, b => Series(1, 3, b));
                lower += exponent * (exponent < 0 ? two.High : two.Low);
                upper += exponent * (exponent < 0 ? two.Low : two.High);
            }
            var a = ExactMeanAccumulator.ScaledRatio(lower, BigInteger.One, -bits);
            var b = ExactMeanAccumulator.ScaledRatio(upper, BigInteger.One, -bits);
            if (a == b) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                return a;
        }
    }
}
