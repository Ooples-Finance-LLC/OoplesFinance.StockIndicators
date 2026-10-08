using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double RoundedIntegerSquareRoot(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        var bits = 0;
        for (var n = value; n > 1; n >>= 1) bits++;
        var grid = 52 - bits / 2;
        var scaled = new BigInteger(value) << (2 * grid);
        var root = BigInteger.One << 53;
        while (true)
        {
            var next = (root + scaled / root) / 2;
            if (next >= root) break;
            root = next;
        }
        // Compare the exact squared midpoint, then apply nearest-even rounding.
        var midpoint = 2 * root + 1;
        var comparison = (4 * scaled).CompareTo(midpoint * midpoint);
        if (comparison > 0 || comparison == 0 && !root.IsEven) root++;
        return (new ReferenceFraction((long)root) / new ReferenceFraction(1L << grid)).ToDouble();
    }

    internal static double[] RoundedSquareRootMean(IReadOnlyList<Bar> bars, int length)
    {
        var weights = Enumerable.Range(1, length).Select(n => ReferenceFraction.FromDouble(RoundedIntegerSquareRoot(n))).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++)
                numerator += ReferenceFraction.FromDouble(bars[i - lag].Close) * weights[length - lag - 1];
            result[i] = (numerator / denominator).ToDouble();
        }
        return result;
    }
}
