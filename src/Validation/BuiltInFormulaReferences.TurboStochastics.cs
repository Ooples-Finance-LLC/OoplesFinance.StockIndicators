using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TurboStochasticsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return TurboStochasticsValues(bars, Integer(o, "Length1", 20), Integer(o, "Length2", 10), Integer(o, "TurboLength", 2), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!, indicator.BatchName == IndicatorName.TurboStochasticsSlow).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) TurboStochasticsValues(IReadOnlyList<Bar> bars, int length, int fitLength, int turbo, MovingAvgType kind, bool slow)
    {
        length = Math.Max(1, length); fitLength = Math.Max(1, fitLength);
        var fitPeriod = Math.Max(1L, (long)fitLength + Math.Max(-(long)fitLength, Math.Min((long)fitLength, turbo)));
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length)
        {
            if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod))
                return Average(values.Select(v => v.ToDouble()).ToArray(), length, kind == MovingAvgType.DoubleExponentialMovingAverage ? 4 : kind == MovingAvgType.TripleExponentialMovingAverage ? 5 : (int)kind).Select(R).ToArray();
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var sum = zero;
                if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
                {
                    for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(kind == MovingAvgType.WeightedMovingAverage ? (long)length - i + j : 1);
                    result[i] = kind == MovingAvgType.WeightedMovingAverage ? R(2) * sum / (R(length) * R(length + 1L)) : i + 1 < length ? zero : sum / R(length);
                }
                else if (kind == MovingAvgType.ExponentialMovingAverage && i < length)
                { for (var j = 0; j <= i; j++) sum += values[j]; result[i] = sum / R(i + 1); }
                else
                { var ema = kind == MovingAvgType.ExponentialMovingAverage; result[i] = ((i == 0 ? zero : result[i - 1]) * R(length - 1) + values[i] * R(ema ? 2 : 1)) / R(ema ? length + 1L : length); }
            }
            return result;
        }
        ReferenceFraction[] Regress(ReferenceFraction[] values)
        {
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var start = (int)Math.Max(0, (long)i - fitPeriod + 1); var n = i - start + 1; var mean = zero;
                for (var j = start; j <= i; j++) mean += values[j]; mean /= R(n);
                var center = R(n - 1) / R(2); var covariance = zero; var variance = zero;
                for (var j = start; j <= i; j++) { var x = R(j - start) - center; covariance += x * (values[j] - mean); variance += x * x; }
                result[i] = n == 1 ? mean : mean + covariance / variance * center;
            }
            return result;
        }
        var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var low = bars[i].Low; var high = bars[i].High;
            for (var j = Math.Max(0, i - length + 1); j < i; j++) { low = Math.Min(low, bars[j].Low); high = Math.Max(high, bars[j].High); }
            var width = R(high) - R(low); var position = width.Sign == 0 ? zero : (R(bars[i].Close) - R(low)) / width * R(100);
            raw[i] = position.Sign < 0 ? zero : position.CompareTo(R(100)) > 0 ? R(100) : position;
        }
        var first = Mean(raw, length); var line = Regress(slow ? first : raw); var signal = Regress(slow ? Mean(first, length) : first); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var difference = line[i] - signal[i]; var previous = i == 0 ? zero : line[i - 1] - signal[i - 1]; var priorLine = i == 0 ? zero : line[i - 1];
            signals[i] = difference.Sign > 0 && difference.CompareTo(previous) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(previous) < 0 ? Signal.StrongSell
                : difference.Sign > 0 || priorLine.CompareTo(R(30)) < 0 && line[i].CompareTo(R(30)) > 0 ? Signal.Buy
                : difference.Sign < 0 || priorLine.CompareTo(R(70)) > 0 && line[i].CompareTo(R(70)) < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Tsf"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
