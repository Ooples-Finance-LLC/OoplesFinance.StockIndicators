using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedVolumeOscillator(IReadOnlyList<Bar> bars, object options)
    {
        var values = bars.Select(b => b.Volume).ToArray();
        var fast = RoundedBoundedStage(values, 5, 1);
        var slow = RoundedBoundedStage(values, Integer(options, "Length", 14), 1);
        return fast.Select((v, i) => slow[i] == 0 ? 0 : (new ReferenceFraction(100) *
            (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(slow[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
    }
}
