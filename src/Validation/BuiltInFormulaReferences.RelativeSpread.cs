using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RelativeSpreadOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions();
        return RelativeSpreadValues(bars, Integer(o, "FastLength", 10), Integer(o, "SlowLength", 40), Integer(o, "Length", 14), Integer(o, "SmoothLength", 5), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!);
    }
    internal static Dictionary<string, double[]> RelativeSpreadValues(IReadOnlyList<Bar> bars, int fast, int slow, int length, int smooth, MovingAvgType kind)
    {
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Average(ReferenceFraction[] values, int period, MovingAvgType averageKind)
        {
            period = Math.Max(1, period);
            if (averageKind is MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage)
            {
                var first = Average(values, period, MovingAvgType.ExponentialMovingAverage); var second = Average(first, period, MovingAvgType.ExponentialMovingAverage);
                var third = averageKind == MovingAvgType.TripleExponentialMovingAverage ? Average(second, period, MovingAvgType.ExponentialMovingAverage) : null;
                return first.Select((v, i) => third is null ? R(2) * v - second[i] : R(3) * (v - second[i]) + third[i]).ToArray();
            }
            var output = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var sum = zero;
                if (averageKind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
                {
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) sum += values[j] * R(averageKind == MovingAvgType.WeightedMovingAverage ? (long)period - i + j : 1);
                    output[i] = averageKind == MovingAvgType.WeightedMovingAverage ? R(2) * sum / (R(period) * R(period + 1L)) : i + 1 < period ? zero : sum / R(period);
                }
                else if (averageKind == MovingAvgType.ExponentialMovingAverage && i < period)
                { for (var j = 0; j <= i; j++) sum += values[j]; output[i] = sum / R(i + 1); }
                else if (averageKind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod)
                { var ema = averageKind == MovingAvgType.ExponentialMovingAverage; output[i] = ((i == 0 ? zero : output[i - 1]) * R(period - 1) + values[i] * R(ema ? 2 : 1)) / R(ema ? period + 1L : period); }
                else throw new NotSupportedException();
            }
            return output;
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var f = Average(prices, fast, kind); var s = Average(prices, slow, kind);
        var changes = prices.Select((_, i) => i == 0 ? zero : (f[i] - s[i]) - (f[i - 1] - s[i - 1])).ToArray();
        var gains = Average(changes.Select(v => v.Sign > 0 ? v : zero).ToArray(), length, MovingAvgType.WildersSmoothingMethod);
        var losses = Average(changes.Select(v => v.Sign < 0 ? zero - v : zero).ToArray(), length, MovingAvgType.WildersSmoothingMethod);
        var rsi = new ReferenceFraction[prices.Length];
        for (var i = 0; i < rsi.Length; i++)
        {
            var value = losses[i].Sign == 0 ? R(100) : R(100) * gains[i] / (gains[i] + losses[i]);
            // The RSI component is exposed to the signal moving average as binary64.
            rsi[i] = i > 0 && Math.Max(1, length) > 1 && changes[i].Sign == 0 ? rsi[i - 1] : R(value.ToDouble());
        }
        return new() { ["Rss"] = Average(rsi, smooth, kind).Select(v => v.ToDouble()).ToArray() };
    }
}
