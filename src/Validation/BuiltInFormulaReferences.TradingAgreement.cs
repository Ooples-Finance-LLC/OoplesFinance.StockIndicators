using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TradingAgreementOutputs(IReadOnlyList<Bar> bars, int length,
        int shortLength = 8, int smoothing = 3, int kind = 2, double threshold = 50, double limit = 0)
    {
        var strength = RoundedPriceRsi(bars, Math.Max(1, length), kind);
        var first = RoundedStochastic(bars, Math.Max(1, shortLength), Math.Max(1, smoothing), 1, kind)["FastD"];
        var second = RoundedStochastic(bars, Math.Max(1, length), Math.Max(1, smoothing), 1, kind)["FastD"];
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var r = strength[i] - threshold; var a = first[i] - threshold; var b = second[i] - threshold;
            result[i] = r > limit && a > limit && b > limit ? b : r < limit && a < limit && b < limit ? -b : 0;
        }
        return Outputs(("Tmmso", result));
    }
}
