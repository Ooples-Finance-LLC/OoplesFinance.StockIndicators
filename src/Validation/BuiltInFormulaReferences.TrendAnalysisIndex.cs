using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendAnalysisIndexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return TrendAnalysisIndexValues(bars, Integer(o, "Length1", 28), Integer(o, "Length2", 5), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) TrendAnalysisIndexValues(IReadOnlyList<Bar> bars, int length1, int length2, MovingAvgType kind)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
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
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = Mean(prices, length1); var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var largest = means[i]; var smallest = means[i];
            for (var j = Math.Max(0, i - length2 + 1); j < i; j++)
            { if (means[j].CompareTo(largest) > 0) largest = means[j]; if (means[j].CompareTo(smallest) < 0) smallest = means[j]; }
            line[i] = prices[i].Sign == 0 ? zero : (R(100) * largest - R(100) * smallest) / prices[i];
        }
        var threshold = Mean(line, length2); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = prices[i] - means[i]; var previous = i == 0 ? zero : prices[i - 1] - means[i - 1];
            signals[i] = line[i].CompareTo(threshold[i]) < 0 ? Signal.None
                : slope.Sign > 0 && slope.CompareTo(previous) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previous) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Tai"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = threshold.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
