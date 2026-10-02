using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SqueezeMomentumOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return SqueezeMomentumValues(bars, Integer(o, "Length", 20), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SqueezeMomentumValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
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
        var mean = Mean(bars.Select(b => R(b.Close)).ToArray());
        var residuals = new ReferenceFraction[bars.Count]; var outputs = new double[bars.Count]; var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var window = bars.Skip(start).Take(i - start + 1).ToArray();
            residuals[i] = R(bars[i].Close) - ((R(window.Max(b => b.High)) + R(window.Min(b => b.Low))) / R(2) + mean[i]) / R(2);
            var n = i - start + 1; var center = R(n - 1) / R(2); var sum = zero; var covariance = zero; var variance = zero;
            for (var j = start; j <= i; j++)
            { var x = R(j - start) - center; sum += residuals[j]; covariance += x * residuals[j]; variance += x * x; }
            var endpoint = sum / R(n) + (variance.Sign == 0 ? zero : covariance / variance * center);
            outputs[i] = endpoint.ToDouble(); var change = endpoint.CompareTo(previous);
            trades[i] = endpoint.Sign > 0 && change > 0 ? Signal.StrongBuy : endpoint.Sign < 0 && change < 0 ? Signal.StrongSell : endpoint.Sign > 0 ? Signal.Buy : endpoint.Sign < 0 ? Signal.Sell : Signal.None;
            previous = endpoint;
        }
        return (new() { ["Smi"] = outputs }, trades);
    }
}
