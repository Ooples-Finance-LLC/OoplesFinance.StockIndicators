using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ResidualVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return ResidualVolatilityValues(bars, Integer(o, "Length", 20), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ResidualVolatilityValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values)
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
        ReferenceFraction Root(ReferenceFraction square)
        {
            if (square.Sign <= 0) return zero;
            var multiplier = R(1); var root = square.SqrtToDouble();
            while (double.IsInfinity(root)) { square /= R(4); multiplier *= R(2); root = square.SqrtToDouble(); }
            return R(root) * multiplier;
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var mean = Mean(prices);
        var residual = prices.Select((v, i) => v - mean[i]).ToArray(); var variance = Mean(residual.Select(v => v * v).ToArray());
        var deviation = variance.Select(Root).ToArray(); var signal = Mean(deviation); var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < trades.Length; i++)
        {
            var change = residual[i].CompareTo(previous);
            trades[i] = deviation[i].CompareTo(signal[i]) < 0 ? Signal.None
                : residual[i].Sign > 0 && change > 0 ? Signal.StrongBuy : residual[i].Sign < 0 && change < 0 ? Signal.StrongSell
                : residual[i].Sign > 0 ? Signal.Buy : residual[i].Sign < 0 ? Signal.Sell : Signal.None;
            previous = residual[i];
        }
        return (new() { ["StdDev"] = deviation.Select(v => v.ToDouble()).ToArray(), ["Variance"] = variance.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
