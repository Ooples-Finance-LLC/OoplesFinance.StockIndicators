using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SupportResistanceOscillatorOutputs(IReadOnlyList<Bar> bars)
        => new Dictionary<string, double[]> { ["Sro"] = SupportResistanceOscillatorValues(bars).Line };
    internal static (double[] Line, Signal[] Signals) SupportResistanceOscillatorValues(IReadOnlyList<Bar> bars)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1); var ratios = new ReferenceFraction[bars.Count]; var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var high = R(bar.High); var low = R(bar.Low); var close = R(bar.Close);
            var priorClose = R(bars[Math.Max(0, i - 1)].Close);
            var ranges = new[] { high - low, (high - priorClose).Abs(), (low - priorClose).Abs() };
            var range = ranges.Aggregate((a, b) => a.CompareTo(b) >= 0 ? a : b);
            // A different grouping: candle width plus signed body, divided by two true ranges.
            var ratio = range.Sign == 0 ? zero : ((high - low) + (close - R(bar.Open))) / (R(2) * range);
            ratios[i] = ratio.Sign < 0 ? zero : ratio.CompareTo(one) > 0 ? one : ratio;
            var previous = i == 0 ? zero : ratios[i - 1]; var older = i < 2 ? zero : ratios[i - 2];
            var slope = ratios[i] - previous; var previousSlope = previous - older;
            trades[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previous.CompareTo(R(.3)) < 0 && ratios[i].CompareTo(R(.3)) > 0 ? Signal.Buy
                : slope.Sign < 0 || previous.CompareTo(R(.7)) > 0 && ratios[i].CompareTo(R(.7)) < 0 ? Signal.Sell : Signal.None;
        }
        return (ratios.Select(v => v.ToDouble()).ToArray(), trades);
    }
}
