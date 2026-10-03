using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? AdaptiveCyberFormulas(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName == IndicatorName.DominantCycleTunedRelativeStrengthIndex)
            return new("DctRsi", new[] { "DctRsi" }, bars => Outputs(("DctRsi", CycleTunedRsiValues(bars, Integer(indicator.CreateOptions(), "Length", 5)))));
        if (indicator.BatchName == IndicatorName.EhlersAdaptiveCyberCycle)
            return new("Eacc", new[] { "Eacc", "Period" }, bars => AdaptiveCyberValues(bars, Integer(indicator.CreateOptions(), "Length", 5), Number(indicator.CreateOptions(), .07, "Alpha")));
        if (indicator.BatchName == IndicatorName.EhlersAdaptiveCenterOfGravityOscillator)
            return new("Eacog", new[] { "Eacog" }, bars => Outputs(("Eacog", AdaptiveGravityValues(bars, Integer(indicator.CreateOptions(), "Length", 5)))));
        return null;
    }
    private static double[] AdaptiveCyberPeriods(double[] prices, int length, double alpha) => AdaptiveCyberReference(prices, length, alpha)["Period"];
}
