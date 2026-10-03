using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedTfsOscillator(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        var kind = BoundedMeanKind(options, 1);
        var fast = RoundedBoundedStage(Closes(bars), Integer(options, "FastLength", 25), kind);
        var slow = RoundedBoundedStage(Closes(bars), Integer(options, "SlowLength", 200), kind);
        var line = fast.Select((v, i) => !percentage ? (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()
            : slow[i] == 0 ? 0 : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(slow[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        var count = line.TakeWhile(v => !double.IsInfinity(v)).Count();
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "SignalLength", 18), kind)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        return Outputs((percentage ? "Ppo" : "TfsMob", line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
    }
}
