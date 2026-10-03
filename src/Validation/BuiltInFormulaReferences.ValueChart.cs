using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ValueChartOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return ValueChartValues(bars, Integer(options, "Length", 5), (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            (indicator as IIndicator)?.Source is not null).Outputs;
    }

    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ValueChartValues(
        IReadOnlyList<Bar> bars, int length, MovingAvgType kind, bool selected = false,
        IReadOnlyList<double>? basisOverride = null, IReadOnlyList<double>? signalOverride = null)
    {
        length = Math.Max(1, length); var rangeLength = Math.Max(2, Math.Min(530, (length + 4L) / 5));
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] source, IReadOnlyList<double>? replacement)
        {
            if (replacement is not null) return Enumerable.Range(0, bars.Count).Select(i => i < replacement.Count ? R(replacement[i]) : zero).ToArray();
            if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod))
                return Average(source.Select(v => v.ToDouble()).ToArray(), length, AverageKind(new { MaType = kind }, 0)).Select(R).ToArray();
            var means = new ReferenceFraction[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var sum = zero;
                if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
                {
                    for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                        sum += source[j] * R(kind == MovingAvgType.WeightedMovingAverage ? (long)length - i + j : 1);
                    means[i] = kind == MovingAvgType.WeightedMovingAverage ? sum * R(2) / (R(length) * R(length + 1L))
                        : i + 1 < length ? zero : sum / R(length);
                }
                else if (kind == MovingAvgType.ExponentialMovingAverage && i < length)
                { for (var j = 0; j <= i; j++) sum += source[j]; means[i] = sum / R(i + 1); }
                else
                {
                    var ema = kind == MovingAvgType.ExponentialMovingAverage;
                    means[i] = ((i == 0 ? zero : means[i - 1]) * R(length - 1) + source[i] * R(ema ? 2 : 1)) / R(ema ? length + 1L : length);
                }
            }
            return means;
        }
        var input = bars.Select(b => selected ? R(b.Close) : (R(b.High) + R(b.Low)) / R(2)).ToArray();
        var highs = new double[bars.Count]; var lows = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var value = input[i].ToDouble();
            var tolerance = 1e-12 * Math.Max(Math.Abs(b.High), Math.Abs(b.Low));
            var inside = value >= b.Low - tolerance && value <= b.High + tolerance;
            var previous = i == 0 ? value : input[i - 1].ToDouble();
            highs[i] = inside ? b.High : Math.Max(previous, value);
            lows[i] = inside ? b.Low : Math.Min(previous, value);
        }
        var basis = Mean(input, basisOverride); var ranges = new ReferenceFraction[bars.Count];
        var coordinates = Enumerable.Range(0, 4).Select(_ => new ReferenceFraction[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var first = (int)Math.Max(0, i - rangeLength + 1);
            var high = highs.Skip(first).Take(i - first + 1).Max(); var low = lows.Skip(first).Take(i - first + 1).Min();
            ranges[i] = R(high) - R(low); var total = zero;
            for (var j = Math.Max(0, i - 4); j <= i; j++) total += ranges[j];
            var prices = new[] { bars[i].Close, bars[i].Open, highs[i], lows[i] };
            for (var slot = 0; slot < 4; slot++) coordinates[slot][i] = total.Sign == 0 ? zero : R(25) * (R(prices[slot]) - basis[i]) / total;
        }
        var signal = Mean(coordinates[0], signalOverride); var trades = new Signal[bars.Count];
        var previousSlope = zero; var previousValue = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var value = coordinates[0][i]; var slope = value - signal[i]; var change = slope.CompareTo(previousSlope);
            trades[i] = slope.Sign > 0 && change > 0 ? Signal.StrongBuy : slope.Sign < 0 && change < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previousValue.CompareTo(R(-4)) < 0 && value.CompareTo(R(-4)) > 0 ? Signal.Buy
                : slope.Sign < 0 || previousValue.CompareTo(R(4)) > 0 && value.CompareTo(R(4)) < 0 ? Signal.Sell : Signal.None;
            previousValue = value; previousSlope = slope;
        }
        var keys = new[] { "vClose", "vOpen", "vHigh", "vLow" };
        return (Enumerable.Range(0, 4).ToDictionary(i => keys[i], i => coordinates[i].Select(v => v.ToDouble()).ToArray()), trades);
    }
}
