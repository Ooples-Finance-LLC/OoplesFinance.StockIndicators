using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedCoefficient(IReadOnlyList<Bar> bars, int length) => bars.Select((_, i) =>
    {
        if (i + 1 < length) return 0d;
        var values = bars.Skip(i - length + 1).Take(length).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var n = new ReferenceFraction(length);
        var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
        if (mean.Sign == 0) return 0d;
        var variance = values.Aggregate(new ReferenceFraction(0), (sum, value) => sum + (value - mean) * (value - mean)) / n;
        return mean.Sign * (variance * new ReferenceFraction(10000) / (mean * mean)).SqrtToDouble();
    }).ToArray();
}
