using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedMacd(IReadOnlyList<Bar> bars, object options)
    {
        var fast = RoundedBoundedStage(Closes(bars), Integer(options, "FastLength", 12), 3);
        var slow = RoundedBoundedStage(Closes(bars), Integer(options, "SlowLength", 26), 3);
        var line = fast.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray();
        var count = Array.FindIndex(line, double.IsInfinity);
        if (count < 0) count = line.Length;
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "SignalLength", 9), 3)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        var histogram = line.Select((v, i) => i >= count ? double.NaN :
            (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Macd", line), ("Signal", signal), ("Histogram", histogram));
    }
}
