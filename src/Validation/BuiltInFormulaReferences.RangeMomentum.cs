using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? RangeMomentum(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        if (indicator.BatchName == IndicatorName.VolatilityBasedMomentum)
        {
            var kind = AverageKind(options, 6);
            if (kind == 0) return null;
            return new("Vbm", new[] { "Vbm", "Signal" }, bars => VolatilityMomentumOutputs(bars, indicator));
        }
        if (indicator.BatchName == IndicatorName.VolatilityQualityIndex)
        {
            var kind = AverageKind(options, 1);
            if (kind == 0) return null;
            return new("Vqi", new[] { "Vqi", "FastSignal", "SlowSignal" }, bars => VolatilityQualityOutputs(bars, indicator));
        }
        return null;
    }
}
