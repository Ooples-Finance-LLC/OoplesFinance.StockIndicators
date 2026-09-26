using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedPopulationVariance(IReadOnlyList<Bar> bars, int length)
        => bars.Select((_, i) =>
        {
            if (i + 1 < length) return 0d;
            var values = bars.Skip(i - length + 1).Take(length).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var mean = values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + v) / new ReferenceFraction(length);
            var squared = values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + (v - mean) * (v - mean));
            return (squared / new ReferenceFraction(length)).ToDouble();
        }).ToArray();
}
