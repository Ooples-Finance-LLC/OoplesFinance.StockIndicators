using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedBollinger(IBuiltInIndicator indicator) =>
        indicator.BatchName is IndicatorName.BollingerBands or IndicatorName.BollingerBandsWidth or IndicatorName.BollingerBandsPercentB
        && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static IReadOnlyDictionary<string, double[]> RoundedBollinger(IReadOnlyList<Bar> bars, int length, int kind, double multiplier)
    {
        var prices = bars.Select(b => b.Close).ToArray();
        var middle = RoundedBoundedStage(prices, length, kind);
        var deviation = PopulationDeviation(prices, length);
        var upper = new double[bars.Count]; var lower = new double[bars.Count];
        var width = new double[bars.Count]; var percent = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var center = ReferenceFraction.FromDouble(middle[i]);
            var spread = ReferenceFraction.FromDouble(deviation[i]) * ReferenceFraction.FromDouble(multiplier);
            var top = center + spread; var bottom = center - spread;
            upper[i] = top.ToDouble(); lower[i] = bottom.ToDouble();
            width[i] = center.Sign == 0 ? 0 : ((top - bottom) / center).ToDouble();
            percent[i] = spread.Sign == 0 ? 0 : ((ReferenceFraction.FromDouble(prices[i]) - bottom) * new ReferenceFraction(100) / (top - bottom)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("BbWidth", width), ("PctB", percent));
    }
}
