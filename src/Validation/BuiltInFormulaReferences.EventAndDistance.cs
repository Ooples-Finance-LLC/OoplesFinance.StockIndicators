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
                return new("Zscore", new[] { "Zscore" }, bars =>
                {
                    var period = Integer(options, "Length", 20);
                    var prices = Closes(bars);
                    var mean = volumeMean ? prices.Select((value, i) =>
                    {
                        var window = Window(bars, i, period).ToArray();
                        var volume = window.Sum(b => b.Volume);
                        return volume == 0 ? 0 : value + window.Sum(b => b.Volume * (b.Close - value)) / volume;
                    }).ToArray() : Average(prices, period, kind);
                    var residual = prices.Zip(mean, (v, m) => v - m).ToArray();
                    var variance = Average(residual.Select(v => v * v).ToArray(), period, 1);
                    return Outputs(("Zscore", residual.Select((v, i) => variance[i] == 0 ? 0 : v / Math.Sqrt(variance[i])).ToArray()));
                });
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
