using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? FourierHarmonicsFormula(IBuiltInIndicator indicator)
        =>indicator.CreateOptions() is EhlersFourierSeriesAnalysisSpecOptions
            ?new("Wave",new[]{"Wave","Roc"},bars=>FourierSeriesOutputs(bars,indicator)):null;
}
