using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? TunedBypassFormula(IBuiltInIndicator indicator)
        =>indicator.CreateOptions() is EhlersDominantCycleTunedBypassFilterSpecOptions?new("V2",new[]{"V1","V2"},bars=>TunedBypassOutputs(bars,indicator)):null;
}
