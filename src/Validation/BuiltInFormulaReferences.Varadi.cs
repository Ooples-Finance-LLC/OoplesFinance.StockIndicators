using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VaradiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VaradiValues(bars, Integer(options, "Length", 14),
            (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            (indicator as IIndicator)?.Source is not null).Outputs;
    }

    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VaradiValues(
        IReadOnlyList<Bar> bars, int length, MovingAvgType kind, bool selected = false,
        IReadOnlyList<double>? replacement = null)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0);
        var ratios = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var high = bar.High; var low = bar.Low;
            if (selected)
            {
                var tolerance = 1e-12 * Math.Max(Math.Abs(high), Math.Abs(low));
                if (!(bar.Close >= low - tolerance && bar.Close <= high + tolerance))
                {
                    var previous = i == 0 ? bar.Close : bars[i - 1].Close;
                    high = Math.Max(bar.Close, previous); low = Math.Min(bar.Close, previous);
                }
            }
            var denominator = R(high) + R(low);
            ratios[i] = denominator.Sign == 0 ? zero : R(bar.Close) * R(2) / denominator;
        }
        var means = new ReferenceFraction[bars.Count];
        if (replacement is not null)
        {
            for (var i = 0; i < means.Length; i++) means[i] = i < replacement.Count ? R(replacement[i]) : zero;
        }
        else if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage
            or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod))
            means = Average(ratios.Select(value => value.ToDouble()).ToArray(), length,
                AverageKind(new { MaType = kind }, 0)).Select(R).ToArray();
        else for (var i = 0; i < means.Length; i++)
        {
            var sum = zero;
            if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            {
                for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                    sum += ratios[j] * R(kind == MovingAvgType.WeightedMovingAverage ? (long)length - i + j : 1);
                means[i] = kind == MovingAvgType.WeightedMovingAverage
                    ? sum * R(2) / (R(length) * R(length + 1L))
                    : i + 1 < length ? zero : sum / R(length);
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage && i < length)
            {
                for (var j = 0; j <= i; j++) sum += ratios[j];
                means[i] = sum / R(i + 1);
            }
            else
            {
                var ema = kind == MovingAvgType.ExponentialMovingAverage;
                means[i] = ((i == 0 ? zero : means[i - 1]) * R(length - 1)
                    + ratios[i] * R(ema ? 2 : 1)) / R(ema ? length + 1L : length);
            }
        }
        var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        long previousCount = 0, previousSlope = 0;
        for (var i = 0; i < values.Length; i++)
        {
            long count = 0;
            for (var j = Math.Max(-1, i - length); j < i; j++)
                if ((j < 0 ? zero : means[j]).CompareTo(means[i]) <= 0) count++;
            values[i] = (R(count * 100) / R(length)).ToDouble();
            var slope = count - previousCount;
            signals[i] = slope > 0 && slope > previousSlope ? Signal.StrongBuy
                : slope < 0 && slope < previousSlope ? Signal.StrongSell
                : slope > 0 ? Signal.Buy : slope < 0 ? Signal.Sell : Signal.None;
            previousCount = count; previousSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Vo"] = values }, signals);
    }
}
