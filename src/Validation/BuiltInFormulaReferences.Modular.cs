using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ModularOutputs(IReadOnlyList<Bar> bars, int length = 200, double beta = .8)
    {
        var alpha = 2d / (Math.Max(1, length) + 1d); var upper = new ReferenceFraction(0); var lower = upper; var side = 0; var result = new double[bars.Count];
        ReferenceFraction Blend(ReferenceFraction a, ReferenceFraction b, double gain) => RoundRocBankStage(RoundRocBankStage(a * ReferenceFraction.FromDouble(gain)) + RoundRocBankStage(b * ReferenceFraction.FromDouble(1 - gain)));
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); if (i == 0) upper = lower = price;
            upper = Blend(price, upper, alpha); lower = Blend(price, lower, alpha);
            if ((price - upper).Sign > 0) upper = price; if ((price - lower).Sign < 0) lower = price;
            side = (price - upper).Sign == 0 ? 1 : (price - lower).Sign == 0 ? 0 : side;
            result[i] = (side == 1 ? Blend(upper, lower, beta) : Blend(lower, upper, beta)).ToDouble();
        }
        return result;
    }
}
