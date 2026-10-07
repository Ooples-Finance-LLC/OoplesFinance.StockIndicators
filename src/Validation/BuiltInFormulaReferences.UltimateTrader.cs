using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] UltimateTraderRaw(IReadOnlyList<Bar> bars, int lookback, int rangeLength)
    {
        lookback = Math.Max(1, lookback); rangeLength = Math.Max(1, rangeLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var ranges = new ReferenceFraction[bars.Count];
        var volume = bars.Select(b => R(b.Volume)).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = R(bars[i == 0 ? 0 : i - 1].Close);
            ranges[i] = new[] { R(bars[i].High) - R(bars[i].Low), (R(bars[i].High) - previous).Abs(),
                (R(bars[i].Low) - previous).Abs() }.OrderBy(v => v).Last();
        }
        ReferenceFraction Position(ReferenceFraction[] values, int i)
        {
            var window = values.Skip(Math.Max(0, i - lookback + 1)).Take(Math.Min(i + 1, lookback)).OrderBy(v => v).ToArray();
            var span = window.Last() - window.First();
            return span.Sign == 0 ? zero : R(100) * (values[i] - window.First()) / span;
        }
        return bars.Select((b, i) =>
        {
            var history = bars.Skip(Math.Max(0, i - rangeLength + 1)).Take(Math.Min(i + 1, rangeLength)).ToArray();
            var low = R(history.Min(v => v.Low)); var extent = R(history.Max(v => v.High)) - low;
            var range = R(b.High) - R(b.Low); var change = R(b.Close) - (i == 0 ? zero : R(bars[i - 1].Close));
            var terms = new[] {
                range.Sign == 0 ? zero : R(100) * (R(b.Close) - R(b.Open)) / range,
                range.Sign == 0 ? zero : R(100) * (R(2) * R(b.Close) - R(b.High) - R(b.Low)) / range,
                change.Sign == 0 || extent.Sign == 0 ? zero : R(100) * (R(2) * (R(b.Close) - low) - extent) / extent,
                extent.Sign == 0 ? zero : R(100) * change / extent,
                R(change.Sign) * Position(ranges, i), R(change.Sign) * Position(volume, i) };
            var bulls = terms.Where(v => v.Sign > 0).Aggregate(zero, (a, v) => a + v);
            var bears = terms.Where(v => v.Sign < 0).Aggregate(zero, (a, v) => a - v);
            return (bulls + bears).Sign == 0 ? 0 : (R(100) * (bulls - bears) / (bulls + bears)).ToDouble();
        }).ToArray();
    }

    internal static IReadOnlyDictionary<string, double[]> UltimateTraderValues(IReadOnlyList<Bar> bars, MovingAvgType kind,
        int lookback = 5, int smoothing = 4, int rangeLength = 2)
    {
        var code = AverageKind(new { MaType = kind }, 0);
        var raw = UltimateTraderRaw(bars, lookback, rangeLength);
        var first = Average(raw, Math.Max(1, lookback), code);
        var line = Average(first, Math.Max(1, smoothing), code);
        return new Dictionary<string, double[]> { ["Uto"] = line, ["Signal"] = Average(line, Math.Max(1, smoothing), code) };
    }
}
