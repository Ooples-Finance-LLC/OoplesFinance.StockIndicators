using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedLeaderOscillator(IReadOnlyList<Bar> bars, object options, bool percentage)
        => LeaderOscillatorReference(bars, options, percentage).Values;

    internal sealed class LeaderReference(IReadOnlyDictionary<string, double[]> values,
        IReadOnlyDictionary<string, double[]> overflowSources)
    {
        internal IReadOnlyDictionary<string, double[]> Values { get; } = values;
        internal IReadOnlyDictionary<string, double[]> OverflowSources { get; } = overflowSources;
    }

    internal static LeaderReference LeaderOscillatorReference(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        // Preserve actual independently rounded upstream infinities at their originating
        // bar. A later NaN suffix is not fresh overflow evidence.
        double[] Sources(params double[][] stages) => Enumerable.Range(0, bars.Count)
            .Select(i => stages.Select(stage => stage[i]).FirstOrDefault(double.IsInfinity)).ToArray();
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
        (double[] Values, double[] Source) Leg(int period)
        {
            var mean = Smooth(values, period);
            var rawResidual = values.Select((v, i) => Difference(v, mean[i])).ToArray();
            var residual = Smooth(rawResidual, period);
            var leg = mean.Select((v, i) => Sum(v, residual[i])).ToArray();
            return (leg, Sources(rawResidual, leg));
        }
        var firstLeg = Leg(Integer(options, "FastLength", 12));
        var first = firstLeg.Values;
        var secondLeg = Leg(Integer(options, "SlowLength", 26));
        var second = secondLeg.Values;
        var line = first.Select((v, i) => !percentage ? Difference(v, second[i]) :
            double.IsNaN(v) || double.IsInfinity(v) || double.IsNaN(second[i]) || double.IsInfinity(second[i]) ? double.NaN :
            second[i] == 0 ? 0 : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(second[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        var lineSource = Sources(firstLeg.Source, secondLeg.Source, line);
        if (!percentage) return new LeaderReference(Outputs(("Macd", line), ("I1", first), ("I2", second)),
            Outputs(("Macd", lineSource), ("I1", firstLeg.Source), ("I2", secondLeg.Source)));
        var signal = Smooth(line, Integer(options, "Length", 9));
        return new LeaderReference(Outputs(("Ppo", line), ("Signal", signal),
            ("Histogram", line.Select((v, i) => Difference(v, signal[i])).ToArray())),
            Outputs(("Ppo", lineSource), ("Signal", lineSource), ("Histogram", lineSource)));
    }
}
