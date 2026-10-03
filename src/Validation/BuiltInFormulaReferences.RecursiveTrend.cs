using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RecursiveTrendOutputs(IReadOnlyList<Bar> bars, int length)
    {
        var alpha = 2d / (Math.Max(1, length) + 1d); var a = ReferenceFraction.FromDouble(alpha); var retained = ReferenceFraction.FromDouble(1 - alpha);
        var accumulators = new ReferenceFraction[bars.Count]; var values = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var oldAccumulator = i == 0 ? price : accumulators[i - 1]; var oldValue = i == 0 ? price : values[i - 1];
            accumulators[i] = RoundRocBankStage(RoundRocBankStage(retained * oldAccumulator) + price);
            var adjusted = RoundRocBankStage(RoundRocBankStage(price + accumulators[i]) - oldAccumulator);
            values[i] = RoundRocBankStage(RoundRocBankStage(retained * oldValue) + RoundRocBankStage(a * adjusted));
        }
        return Outputs(("Rmta", values.Select(v => v.ToDouble()).ToArray()));
    }
}
