using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedLeaderOscillator(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        var kind = BoundedMeanKind(options, 3);
        double[] Smooth(double[] values, int period)
        {
            var count = values.TakeWhile(v => !double.IsNaN(v) && !double.IsInfinity(v)).Count();
            return RoundedBoundedStage(values.Take(count).ToArray(), period, kind)
                .Concat(Enumerable.Repeat(double.NaN, values.Length - count)).ToArray();
        }
        double Difference(double left, double right) => double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(right) || double.IsInfinity(right)
            ? left - right : (ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right)).ToDouble();
        double Sum(double left, double right) => double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(right) || double.IsInfinity(right)
            ? left + right : (ReferenceFraction.FromDouble(left) + ReferenceFraction.FromDouble(right)).ToDouble();
        var values = Closes(bars);
        double[] Leg(int period)
        {
            var mean = Smooth(values, period);
            var residual = Smooth(values.Select((v, i) => Difference(v, mean[i])).ToArray(), period);
            return mean.Select((v, i) => Sum(v, residual[i])).ToArray();
        }
        var first = Leg(Integer(options, "FastLength", 12));
        var second = Leg(Integer(options, "SlowLength", 26));
        var line = first.Select((v, i) => !percentage ? Difference(v, second[i]) :
            double.IsNaN(v) || double.IsInfinity(v) || double.IsNaN(second[i]) || double.IsInfinity(second[i]) ? double.NaN :
            second[i] == 0 ? 0 : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(second[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        if (!percentage) return Outputs(("Macd", line), ("I1", first), ("I2", second));
        var signal = Smooth(line, Integer(options, "Length", 9));
        return Outputs(("Ppo", line), ("Signal", signal), ("Histogram", line.Select((v, i) => Difference(v, signal[i])).ToArray()));
    }
}
