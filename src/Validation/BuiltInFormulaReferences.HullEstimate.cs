using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HullEstimateOutputs(IReadOnlyList<Bar> bars, int length)
    {
        var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var weighted = SmoothRocBankStage(prices, period, 2); var exponential = SmoothRocBankStage(prices, period, 3);
        return Outputs(("He", weighted.Select((v, i) => (new ReferenceFraction(3) * v - new ReferenceFraction(2) * exponential[i]).ToDouble()).ToArray()));
    }
}
