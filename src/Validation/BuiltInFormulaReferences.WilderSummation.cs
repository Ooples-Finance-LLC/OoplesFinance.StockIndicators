using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WilderSummationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = new ReferenceFraction(Integer(indicator.CreateOptions(), "Length", 14));
        var previous = new ReferenceFraction(0);
        var values = new double[bars.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var decay = RoundRocBankStage(previous / length);
            var retained = RoundRocBankStage(previous - decay);
            previous = RoundRocBankStage(retained + ReferenceFraction.FromDouble(bars[i].Close));
            values[i] = previous.ToDouble();
        }
        return Outputs(("Wws", values));
    }
}
