using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? RemainingTrends(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is not (IndicatorName.ModifiedGannHiloActivator or IndicatorName.RobustWeightingOscillator or IndicatorName.R2AdaptiveRegression or IndicatorName.RecursiveRelativeStrengthIndex or IndicatorName.PercentageTrend)) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length");
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        switch (indicator.BatchName)
        {
            case IndicatorName.ModifiedGannHiloActivator:
                return new("Ghla", new[] { "Ghla" }, bars => ModifiedGannOutputs(bars, indicator));
            case IndicatorName.PercentageTrend:
                return new("Pti", new[] { "Pti" }, bars =>
                {
                    var prices = Closes(bars); var percentage = Number(options, .15, "Pct");
                    double At(int i) => i < 0 ? 0 : prices[i];
                    return Outputs(("Pti", prices.Select((price, i) =>
                    {
                        var line = price; var span = 0;
                        for (var lag = 1; lag <= length; lag++)
                        {
                            var older = At(i-lag); var newer = At(i-lag+1);
                            if (newer <= line && older > line || newer >= line && older < line) span = 0;
                            // The retained span always samples the suffix ending at the
                            // current bar; include the inspected historical observation.
                            var candidates = Enumerable.Range(i-span, span+1).Select(At).Append(older);
                            line = older > line ? candidates.Max()*(1-percentage) : candidates.Min()*(1+percentage);
                            span++;
                        }
                        return line;
                    }).ToArray()));
                });
            case IndicatorName.RecursiveRelativeStrengthIndex:
                return new("Rrsi", new[] { "Rrsi" }, bars => RecursiveRsiOutputs(bars, indicator));
            case IndicatorName.R2AdaptiveRegression:
                return new("R2ar",new[]{"R2ar"},bars=>R2AdaptiveOutputs(bars,indicator));
            case IndicatorName.RobustWeightingOscillator:
                return new("Rwo", new[] { "Rwo" }, bars => RobustWeightingOutputs(bars, indicator));
            default: return null;
        }
    }
}
