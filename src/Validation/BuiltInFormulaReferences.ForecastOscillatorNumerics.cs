using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedForecastOscillator(IReadOnlyList<Bar> bars, int length) => bars.Select((bar, i) =>
    {
        if (bar.Close == 0) return 0d;
        var count = Math.Min(i + 1, length);
        var values = bars.Skip(i - count + 1).Take(count).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / new ReferenceFraction(count);
        var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
        var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
        for (var j = 0; j < count; j++)
        {
            var position = new ReferenceFraction(j) - center;
            xx += position * position; xy += position * (values[j] - mean);
        }
        var endpoint = count == 1 ? mean : mean + xy / xx * center;
        var price = ReferenceFraction.FromDouble(bar.Close);
        return ((price - endpoint) / price * new ReferenceFraction(100)).ToDouble();
    }).ToArray();
}
