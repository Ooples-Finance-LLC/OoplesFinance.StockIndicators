using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedErgodic(IReadOnlyList<Bar> bars, object options)
    {
        if (options is ErgodicPercentagePriceOscillatorSpecOptions ppo)
            return RoundedPpo(bars, new PpoSpecOptions(32, ppo.Length, ppo.MaType, 5));
        var fast = RoundedBoundedStage(Closes(bars), Integer(options, "Length1", 32), BoundedMeanKind(options, 3));
        var slow = RoundedBoundedStage(Closes(bars), Integer(options, "Length2", 5), BoundedMeanKind(options, 3));
        var line = fast.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray();
        var count = Array.FindIndex(line, double.IsInfinity);
        if (count < 0) count = line.Length;
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "Length3", 5), BoundedMeanKind(options, 3))
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        var histogram = line.Select((v, i) => i >= count ? double.NaN :
            (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Macd", line), ("Signal", signal), ("Histogram", histogram));
    }
}
