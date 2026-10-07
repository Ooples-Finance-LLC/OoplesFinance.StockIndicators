using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? CandleTrends(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName == IndicatorName.VervoortModifiedBollingerBandIndicator && AverageKind(indicator.CreateOptions(),5)!=0)
            return new("MiddleBand",new[]{"UpperBand","MiddleBand","LowerBand","PercentB"},bars=>VervoortModifiedOutputs(bars,indicator));
        if (indicator.BatchName == IndicatorName.VervoortSmoothedOscillator)
            return new("Vso",new[]{"Vso","Sk"},bars=>VervoortSmoothedOutputs(bars,indicator));

        var longTerm = indicator.BatchName == IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;
        if (!longTerm && indicator.BatchName != IndicatorName.VervoortHeikenAshiCandlestickOscillator) return null;
        var key = longTerm ? "Vhaltco" : "Vhaco";
        return new(key,new[]{key},bars=>VervoortCandleOutputs(bars,indicator));
    }
}
