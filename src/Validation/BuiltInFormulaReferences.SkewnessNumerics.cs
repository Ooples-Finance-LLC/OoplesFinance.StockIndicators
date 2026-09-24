using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedSkewness(IReadOnlyList<Bar> bars, int length) => bars.Select((_, i) =>
    {
        if (i + 1 < length) return 0d;
        var values = bars.Skip(i - length + 1).Take(length).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var n = new ReferenceFraction(length);
        var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
        var second = new ReferenceFraction(0); var third = new ReferenceFraction(0);
        foreach (var value in values)
        {
            var centered = value - mean;
            second += centered * centered; third += centered * centered * centered;
        }
        if (second.Sign == 0 || third.Sign == 0) return 0d;
        var variance = second / n; var moment = third / n;
        return moment.Sign * (moment * moment / (variance * variance * variance)).SqrtToDouble();
    }).ToArray();
}
