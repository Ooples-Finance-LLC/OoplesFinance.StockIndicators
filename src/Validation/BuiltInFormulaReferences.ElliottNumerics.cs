using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedElliott(IReadOnlyList<Bar> bars, object options)
    {
        var fast = RoundedBoundedStage(Closes(bars), Integer(options, "FastLength", 5), BoundedMeanKind(options, 1));
        var slow = RoundedBoundedStage(Closes(bars), Integer(options, "SlowLength", 34), BoundedMeanKind(options, 1));
        var line = fast.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray();
        var count = Array.FindIndex(line, double.IsInfinity);
        if (count < 0) count = line.Length;
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "FastLength", 5), BoundedMeanKind(options, 1))
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        var histogram = line.Select((v, i) => i >= count ? double.NaN :
            (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Ewo", line), ("Signal", signal), ("Histogram", histogram));
    }
}
