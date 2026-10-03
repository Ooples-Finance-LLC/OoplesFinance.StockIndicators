using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedRootMeanSquare(IReadOnlyList<Bar> bars, int length)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var squares = new ReferenceFraction(0);
            var count = Math.Min(length, i + 1);
            for (var j = i - count + 1; j <= i; j++)
            {
                var value = ReferenceFraction.FromDouble(bars[j].Close);
                squares += value * value;
            }
            // Binary-search adjacent representable outputs and their squared
            // midpoint; the production path uses scaled integer square roots.
            result[i] = (squares / new ReferenceFraction(count)).SqrtToDouble();
        }
        return result;
    }
}
