using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> EarningLevelsOutputs(IReadOnlyList<Bar> bars)
        => new Dictionary<string,double[]> { ["Esr"] = bars.Select((b,i) => ExactPriceMean(b.High, i >= 2 ? bars[i-2].Low : 0)).ToArray() };
}
