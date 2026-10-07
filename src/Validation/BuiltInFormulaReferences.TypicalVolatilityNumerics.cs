using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedTypicalVolatility(IReadOnlyList<Bar> bars, int length)
    {
        var typical = bars.Select(b => ReferenceFraction.FromDouble(((ReferenceFraction.FromDouble(b.High)
            + ReferenceFraction.FromDouble(b.Low) + ReferenceFraction.FromDouble(b.Close)) / new ReferenceFraction(3)).ToDouble())).ToArray();
        return bars.Select((_, i) =>
        {
            if (i + 1 < length) return 0d;
            var values = typical.Skip(i - length + 1).Take(length).ToArray();
            var n = new ReferenceFraction(length);
            var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            return (values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + (v - mean) * (v - mean)) / n).SqrtToDouble();
        }).ToArray();
    }
}
