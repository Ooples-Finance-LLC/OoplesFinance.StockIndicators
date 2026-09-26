using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DemarkPressureOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var second = indicator.BatchName == IndicatorName.DemarkPressureRatioV2;
        var length = Integer(indicator.CreateOptions(), "Length", second ? 10 : 13);
        var buying = new ReferenceFraction[bars.Count]; var selling = new ReferenceFraction[bars.Count];
        var zero = new ReferenceFraction(0); var hundred = new ReferenceFraction(100); var threshold = new ReferenceFraction(3) / new ReferenceFraction(20);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var open = ReferenceFraction.FromDouble(b.Open); var close = ReferenceFraction.FromDouble(b.Close);
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var volume = ReferenceFraction.FromDouble(b.Volume);
            var previous = ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Close); var delta = close - open;
            if (second)
            {
                var range = high - low; var weighted = range.Sign == 0 ? zero : delta * volume / range;
                buying[i] = delta.Sign > 0 ? weighted : zero; selling[i] = delta.Sign < 0 ? weighted : zero;
            }
            else
            {
                var up = previous.Sign == 0 ? zero : (open - previous) / previous;
                var down = open.Sign == 0 ? zero : (previous - open) / open;
                buying[i] = volume * (up.CompareTo(threshold) > 0 ? high - previous + close - low : delta.Sign > 0 ? delta : zero);
                selling[i] = volume * (down.CompareTo(threshold) > 0 ? zero - (previous - low + high - close) : delta.Sign < 0 ? delta : zero);
            }
        }
        var output = bars.Select((_, i) =>
        {
            var buy = Window(buying, i, length).Aggregate(zero, (a, b) => a + b);
            var sell = Window(selling, i, length).Aggregate(zero, (a, b) => a + b);
            var denominator = second ? buy + sell.Abs() : buy - sell;
            if (denominator.Sign == 0) return second ? 50d : 0d;
            var percentage = hundred * buy / denominator;
            return percentage.Sign < 0 ? 0 : percentage.CompareTo(hundred) > 0 ? 100 : percentage.ToDouble();
        }).ToArray();
        return Outputs(("Dpr", output));
    }
}
