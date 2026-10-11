using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Directed fixed-point atanh intervals certify the rounded logarithm before
// dispersion. Ordinary log approximations can erase or invent tiny variance.
internal static class CertifiedLogReturn
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        int,
        (BigInteger Low, BigInteger High)
    > LogTwo = new();

    private static BigInteger Ceiling(BigInteger n, BigInteger d) => (n + d - 1) / d;

    private static (BigInteger Low, BigInteger High) Series(BigInteger n, BigInteger d, int bits)
    {
        if (n.IsZero)
            return (0, 0);
        var scale = BigInteger.One << bits;
        var low = n * scale / d;
        var high = Ceiling(n * scale, d);
        var squareLow = low * low / scale;
        var squareHigh = Ceiling(high * high, scale);
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
            powerLow = powerLow * squareLow / scale;
            powerHigh = Ceiling(powerHigh * squareHigh, scale);
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
        for (var bits = 96; ; bits *= 2)
        {
            var core = Series(n - d, n + d, bits);
            var two = LogTwo.GetOrAdd(bits, b => Series(1, 3, b));
            var lower = core.Low + exponent * (exponent < 0 ? two.High : two.Low);
            var upper = core.High + exponent * (exponent < 0 ? two.Low : two.High);
            var a = ExactMeanAccumulator.ScaledRatio(lower, BigInteger.One, -bits);
            var b = ExactMeanAccumulator.ScaledRatio(upper, BigInteger.One, -bits);
            if (a == b) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                return a;
        }
    }
}
