using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TStepLeastSquaresOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Tslsma"] = TStepLeastSquaresValues(bars, Integer(o, "Length", 100), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Line }; }
    internal static (double[] Line, Signal[] Signals, double[] Steps) TStepLeastSquaresValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction Abs(ReferenceFraction x) => x.Sign < 0 ? zero - x : x;
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
        var prices = bars.Select(b => R(b.Close)).ToArray(); var steps = new ReferenceFraction[bars.Count]; var distances = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? prices[i] : steps[i - 1]; var efficiency = zero;
            if (i >= length)
            {
                var travel = zero; for (var j = i - length + 1; j <= i; j++) travel += Abs(prices[j] - prices[j - 1]);
                if (travel.Sign != 0) efficiency = Abs(prices[i] - prices[i - length]) / travel;
            }
            distances[i] = Abs(prices[i] - previous); var distanceTotal = zero; for (var j = 0; j <= i; j++) distanceTotal += distances[j];
            var width = distanceTotal * (R(2) - efficiency) / R(i + 1);
            steps[i] = prices[i].CompareTo(previous - width) < 0 || prices[i].CompareTo(previous + width) > 0 ? prices[i] : previous;
        }
        var means = Mean(prices, length); var stepMeans = Mean(steps, length); var line = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = zero;
            if (i + 1 >= length)
            {
                var centerX = zero; var centerY = zero; for (var j = i - length + 1; j <= i; j++) { centerX += steps[j]; centerY += prices[j]; }
                centerX /= R(length); centerY /= R(length); var covariance = zero; var variance = zero;
                for (var j = i - length + 1; j <= i; j++) { var x = steps[j] - centerX; covariance += x * (prices[j] - centerY); variance += x * x; }
                if (variance.Sign != 0) slope = covariance / variance;
            }
            line[i] = means[i] + slope * (steps[i] - stepMeans[i]);
            var comparison = prices[i] - line[i]; var previous = i == 0 ? zero - prices[i] : prices[i - 1] - line[i - 1];
            signals[i] = comparison.Sign > 0 && comparison.CompareTo(previous) > 0 ? Signal.StrongBuy : comparison.Sign < 0 && comparison.CompareTo(previous) < 0 ? Signal.StrongSell
                : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (line.Select(v => v.ToDouble()).ToArray(), signals, steps.Select(v => v.ToDouble()).ToArray());
    }
}
