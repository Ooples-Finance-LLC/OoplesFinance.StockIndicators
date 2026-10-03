using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WaveTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => WaveTrendValues(bars, MovingAvgType.ExponentialMovingAverage, Integer(indicator.CreateOptions(), "Length", 10), 21, 4,
            indicator is IIndicator value && value.Source is not null).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) WaveTrendValues(IReadOnlyList<Bar> bars, MovingAvgType kind,
        int channel, int average, int signal, bool selected = false, bool hlc = false)
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
        var prices = bars.Select(b => selected ? R(b.Close) : hlc ? (R(b.High) + R(b.Low) + R(b.Close)) / R(3)
            : (R(b.Open) + R(b.High) + R(b.Low) + R(b.Close)) / R(4)).ToArray();
        var basis = Mean(prices, channel); var residual = prices.Select((v, i) => v - basis[i]).ToArray();
        var deviation = Mean(residual.Select(v => v.Abs()).ToArray(), channel);
        var normalized = residual.Select((v, i) => deviation[i].Sign == 0 ? R(0) : (v / deviation[i]) / R(.015)).ToArray();
        var line = Mean(normalized, average); var trigger = Mean(line, signal); var trades = new Signal[prices.Length];
        for (var i = 0; i < trades.Length; i++)
        {
            var previous = i == 0 ? R(0) : line[i - 1]; var margin = line[i] - trigger[i];
            var before = i == 0 ? R(0) : line[i - 1] - trigger[i - 1];
            if (margin.Sign > 0 && margin.CompareTo(before) > 0) trades[i] = Signal.StrongBuy;
            else if (margin.Sign < 0 && margin.CompareTo(before) < 0) trades[i] = Signal.StrongSell;
            else if (margin.Sign > 0 || previous.CompareTo(R(-53)) < 0 && line[i].CompareTo(R(-53)) > 0) trades[i] = Signal.Buy;
            else if (margin.Sign < 0 || previous.CompareTo(R(53)) > 0 && line[i].CompareTo(R(53)) < 0) trades[i] = Signal.Sell;
        }
        return (new Dictionary<string, double[]> { ["Wto"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = trigger.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
