using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasBoundedStochastic(IBuiltInIndicator indicator) => (indicator.BatchName is IndicatorName.StochasticOscillator or IndicatorName.StochasticRegular or IndicatorName.DynamicMomentumOscillator or IndicatorName.DoubleStochasticOscillator or IndicatorName.StochasticFastOscillator)
        && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19;

    internal static double[] RoundedStochasticK(IReadOnlyList<Bar> bars, int length)
        => Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
            var lower = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            var upper = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var range = upper - lower;
            return range.Sign == 0 ? 0 : Math.Max(0, Math.Min(100,
                (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(bars[i].Close) - lower) / range).ToDouble()));
        }).ToArray();

    internal static IReadOnlyDictionary<string, double[]> RoundedStochastic(IReadOnlyList<Bar> bars, int length,
        int smooth1, int smooth2, int kind)
    {
        var raw = RoundedStochasticK(bars, length);
        var first = RoundedBoundedStage(raw, smooth1, kind);
        return Outputs(("FastK", raw), ("FastD", first), ("SlowD", RoundedBoundedStage(first, smooth2, kind)));
    }

    internal static double[] RoundedRateOfChange(IReadOnlyList<Bar> bars, int length, bool volume = false)
    {
        var values = bars.Select(b => volume ? b.Volume : b.Close).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i => i < length || values[i - length] == 0 ? 0
            : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(values[i])
                - ReferenceFraction.FromDouble(values[i - length]))
                / ReferenceFraction.FromDouble(values[i - length])).ToDouble()).ToArray();
    }

    internal static double[] RoundedWilliams(IReadOnlyList<Bar> bars, int length)
        => Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
            var upper = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var lower = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            return upper.CompareTo(lower) == 0 ? -100 : (new ReferenceFraction(-100)
                * (upper - ReferenceFraction.FromDouble(bars[i].Close)) / (upper - lower)).ToDouble();
        }).ToArray();

    internal static IReadOnlyDictionary<string, double[]> RoundedDoubleStochastic(IReadOnlyList<Bar> bars, int length,
        int kind, int smoothing = 3)
    {
        var raw = RoundedStochasticK(bars, length);
        var period = Math.Max(2, length);
        var normalized = Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = raw.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(period, i + 1)).ToArray();
            var lower = ReferenceFraction.FromDouble(window.Min());
            var width = ReferenceFraction.FromDouble(window.Max()) - lower;
            return width.Sign == 0 ? 0 : (new ReferenceFraction(100)
                * (ReferenceFraction.FromDouble(raw[i]) - lower) / width).ToDouble();
        }).ToArray();
        var line = RoundedBoundedStage(normalized, smoothing, kind);
        return Outputs(("Dso", line), ("Signal", RoundedBoundedStage(line, smoothing, kind)));
    }

    internal static double[] RoundedDynamicMomentum(IReadOnlyList<Bar> bars, int length, int kind, int slowPeriod = 20)
    {
        var stages = RoundedStochastic(bars, length, length, slowPeriod, kind);
        var fast = stages["FastD"];
        var slow = stages["SlowD"];
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            // Recompute each prefix's extrema independently of the production running state.
            var prefix = fast.Take(i + 1).ToArray();
            var sum = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(prefix.Min())
                + ReferenceFraction.FromDouble(Math.Max(0, prefix.Max()))).ToDouble());
            var midpoint = ReferenceFraction.FromDouble((sum / new ReferenceFraction(2)).ToDouble());
            var gap = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(slow[i])
                - ReferenceFraction.FromDouble(fast[i])).ToDouble());
            return Math.Max(0, Math.Min(100, (midpoint - gap).ToDouble()));
        }).ToArray();
    }

    internal static IReadOnlyDictionary<string, double[]> RoundedDiNapoli(IReadOnlyList<Bar> bars, int length,
        int first = 3, int second = 3)
    {
        double[] Stage(double[] input, int period)
        {
            var output = new double[input.Length];
            var previous = new ReferenceFraction(0);
            for (var i = 0; i < input.Length; i++)
            {
                var difference = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(input[i]) - previous).ToDouble());
                var increment = ReferenceFraction.FromDouble((difference / new ReferenceFraction(period)).ToDouble());
                output[i] = (previous + increment).ToDouble();
                previous = ReferenceFraction.FromDouble(output[i]);
            }
            return output;
        }
        var line = Stage(RoundedStochasticK(bars, length), first);
        return Outputs(("Dpso", line), ("Signal", Stage(line, second)));
    }

    private static IEnumerable<IndicatorValidationRule> StochasticNumericalRules(IIndicator indicator, IBuiltInIndicator builtIn)
    {
        var options = builtIn.CreateOptions();
        if (builtIn.BatchName == IndicatorName.DoubleStochasticOscillator)
        {
            var period = Integer(options, "Length", 14);
            var averageKind = BoundedMeanKind(options, 1);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedDoubleStochastic(bars, period, averageKind)["Dso"], IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1, bars => RoundedDoubleStochastic(bars, period, averageKind)["Signal"], IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DynamicMomentumOscillator)
        {
            yield return IndicatorValidationRule.Reference(0, bars => RoundedDynamicMomentum(bars,
                Integer(options, "Length", 10), BoundedMeanKind(options, 1)), IndicatorErrorBudget.Exact);
            yield break;
        }
        var regular = builtIn.BatchName == IndicatorName.StochasticRegular;
        var length = regular ? Integer(options, "Length1", 5) : options is StochasticSpecOptions compact ? compact.KLength : Integer(options, "Length", 14);
        var smooth1 = regular ? Integer(options, "Length2", 3) : options is StochasticSpecOptions compactSignal ? compactSignal.DLength : Integer(options, "SmoothLength1", 3);
        var smooth2 = Integer(options, "SmoothLength2", 3);
        var kind = BoundedMeanKind(options, 1);
        for (var slot = 0; slot < indicator.Outputs.Count; slot++)
        {
            var key = builtIn.BatchName == IndicatorName.StochasticFastOscillator ? slot == 0 ? "FastD" : "SlowD"
                : slot == 0 ? builtIn.BatchOutputKey == "FastD" ? "FastD" : "FastK" : slot == 1 ? "FastD" : "SlowD";
            yield return IndicatorValidationRule.Reference(slot, bars => RoundedStochastic(bars, length, smooth1, smooth2, kind)[key], IndicatorErrorBudget.Exact);
        }
    }
}
