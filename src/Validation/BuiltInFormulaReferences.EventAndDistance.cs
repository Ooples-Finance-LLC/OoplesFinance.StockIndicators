using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? EventAndDistance(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        switch (indicator.BatchName)
        {
            case IndicatorName.ZDistanceFromVwap:
                var volumeMean = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.VolumeWeightedAveragePrice;
                if (kind == 0 && !volumeMean) return null;
                return new("Zscore", new[] { "Zscore" }, bars => ZDistanceOutputs(bars, indicator));
            case IndicatorName.DrunkardWalk:
                return new("UpWalk", new[] { "UpWalk", "DnWalk" }, bars => DrunkardWalkOutputs(bars, indicator));
            case IndicatorName.WellesWilderVolatilitySystem:
                if (kind == 0) return null;
                return new("Wwvs", new[] { "Wwvs" }, bars => WilderVolatilityOutputs(bars, indicator));
            case IndicatorName.UtBotAlerts:
                if (kind == 0) return null;
                return new("TrailingStop", new[] { "TrailingStop", "Position", "Buy", "Sell" }, bars => UtBotOutputs(bars, indicator));
            default: return null;
        }
    }
}
