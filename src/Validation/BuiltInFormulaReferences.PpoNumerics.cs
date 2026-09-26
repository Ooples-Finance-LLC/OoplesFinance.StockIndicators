using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedPpo(IReadOnlyList<Bar> bars, object options)
    {
        var kind = BoundedMeanKind(options, 3);
        var fast = options is PriceOscillatorPercentSpecOptions p ? p.ShortLength : Integer(options, "FastLength", 12);
        var slow = options is PriceOscillatorPercentSpecOptions q ? q.LongLength : Integer(options, "SlowLength", 26);
        var first = RoundedBoundedStage(Closes(bars), fast, kind);
        var second = RoundedBoundedStage(Closes(bars), slow, kind);
        var line = first.Select((value, i) => second[i] == 0 ? 0 : (new ReferenceFraction(100) *
            (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(second[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        var count = Array.FindIndex(line, double.IsInfinity);
        if (count < 0) count = line.Length;
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "SignalLength", 9), kind)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        var histogram = line.Select((value, i) => i >= count ? double.NaN :
            (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Ppo", line), ("Signal", signal), ("Histogram", histogram));
    }
}
