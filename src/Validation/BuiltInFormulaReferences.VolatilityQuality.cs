using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolatilityQualityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VolatilityQualityValues(bars, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            Integer(options, "FastLength", 9), Integer(options, "SlowLength", 200), indicator is IIndicator value && value.Source is not null).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VolatilityQualityValues(IReadOnlyList<Bar> bars,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int fast = 9, int slow = 200, bool selected = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length)
        {
            length = Math.Max(1, length);
            if (code is 3 or 6)
            {
                // Recursive means do not need cumulative sums of normalized ratios.
                // Expand the update as previous + alpha*(input-previous), independently of production's weighted numerator.
                var output = new ReferenceFraction[values.Length]; var seed = R(0); var previous = R(0);
                var alpha = R(code == 3 ? 2 : 1) / R(code == 3 ? length + 1L : length);
                for (var i = 0; i < values.Length; i++)
                {
                    if (code == 3 && i < length) { seed += values[i]; previous = seed / R(i + 1L); }
                    else previous += alpha * (values[i] - previous);
                    output[i] = previous;
                }
                return output;
            }
            if (code is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), length, code).Select(R).ToArray();
            return values.Select((_, i) =>
            {
                if (code == 1 && i + 1 < length) return R(0);
                var sum = R(0);
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(code == 2 ? length - (long)i + j : 1);
                return sum / (code == 2 ? R(length) * R(length + 1L) / R(2) : R(length));
            }).ToArray();
        }
        var line = new ReferenceFraction[bars.Count]; var quality = R(0); var total = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var previous = i == 0 ? b.Close : bars[i - 1].Close; var high = b.High; var low = b.Low;
            if (selected)
            {
                var tolerance = 1e-12 * Math.Max(Math.Abs(high), Math.Abs(low));
                if (!(b.Close >= low - tolerance && b.Close <= high + tolerance)) { high = Math.Max(b.Close, previous); low = Math.Min(b.Close, previous); }
            }
            var range = R(high) - R(low); var change = R(b.Close) - R(previous); var body = R(b.Close) - R(b.Open);
            var tr = new[] { range, (R(high) - R(previous)).Abs(), (R(low) - R(previous)).Abs() }.Max();
            // Combine the two normalized terms into one rational numerator independently of production.
            if (range.Sign != 0 && tr.Sign != 0) quality = (change * range + body * tr) / (R(2) * tr * range);
            total += quality.Abs() * (change + body) / R(2); line[i] = total;
        }
        var first = Mean(line, fast); var second = Mean(line, slow); var previousMargin = R(0); var trades = new Signal[line.Length];
        for (var i = 0; i < line.Length; i++)
        {
            var margin = line[i] - first[i];
            trades[i] = margin.Sign > 0 && margin.CompareTo(previousMargin) > 0 ? Signal.StrongBuy
                : margin.Sign < 0 && margin.CompareTo(previousMargin) < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previousMargin = margin;
        }
        return (new Dictionary<string, double[]> { ["Vqi"] = line.Select(v => v.ToDouble()).ToArray(), ["FastSignal"] = first.Select(v => v.ToDouble()).ToArray(),
            ["SlowSignal"] = second.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
