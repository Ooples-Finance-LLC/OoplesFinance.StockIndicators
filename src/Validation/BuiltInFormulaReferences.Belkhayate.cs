using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] BelkhayateOutputs(IReadOnlyList<Bar> bars)
    {
        var middle = bars.Select(b => RoundRocBankStage((ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2))).ToArray();
        var ranges = bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var centerSum = new ReferenceFraction(0); var rangeSum = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - 4); j <= i; j++) { centerSum += middle[j]; rangeSum += ranges[j]; }
            var center = RoundRocBankStage(centerSum / new ReferenceFraction(5));
            var scale = RoundRocBankStage(RoundRocBankStage(rangeSum / new ReferenceFraction(5)) * ReferenceFraction.FromDouble(.2));
            return scale.Sign == 0 ? 0 : (RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - center) / scale).ToDouble();
        }).ToArray();
    }
}
