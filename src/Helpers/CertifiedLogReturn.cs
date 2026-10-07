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
        }
        var tail = Ceiling(powerHigh * scale, (2 * terms + 1) * (scale - squareHigh));
        return (2 * sumLow, 2 * (sumHigh + tail));
    }

    internal static double Of(double current, double previous)
    {
        var n = ExactVarianceWindow.Units(Math.Abs(current));
        var d = ExactVarianceWindow.Units(Math.Abs(previous));
        if (n == d)
            return 0;
        var exponent = (int)(n.GetBitLength() - d.GetBitLength());
        if (exponent >= 0 ? n < (d << exponent) : (n << -exponent) < d)
            exponent--;
        if (exponent < 0)
            n <<= -exponent;
        else
            d <<= exponent;
        for (var bits = 192; ; bits *= 2)
        {
            var scale = BigInteger.One << bits;
            var core = Series(n - d, n + d, bits);
            var two = LogTwo.GetOrAdd(bits, b => Series(1, 3, b));
            var lower = core.Low + exponent * (exponent < 0 ? two.High : two.Low);
            var upper = core.High + exponent * (exponent < 0 ? two.Low : two.High);
            var a = ExactMeanAccumulator.UnitRatio(lower << 1074, scale);
            var b = ExactMeanAccumulator.UnitRatio(upper << 1074, scale);
            if (a == b) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                return a;
        }
    }
}
