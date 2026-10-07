using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] AverageErrorOutputs(IReadOnlyList<Bar> bars, int length = 27)
    {
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(1, length); var decay = Math.Exp(Math.Max(-.99, Math.Min(-.01, -angle)));
        var c2 = 2 * decay * Math.Cos(Math.Max(.01, Math.Min(.99, angle))); var c3 = -1 * decay * decay; var c1 = 1 - c2 - c3;
        var smooth = new ReferenceFraction[bars.Count]; var error = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction v, double factor) => Round(v * ReferenceFraction.FromDouble(factor));
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            if (i < 3) { smooth[i] = price; error[i] = new ReferenceFraction(0); result[i] = bars[i].Close; continue; }
            var pair = Round(price + ReferenceFraction.FromDouble(bars[i - 1].Close));
            smooth[i] = Round(Round(Product(pair, .5 * c1) + Product(smooth[i - 1], c2)) + Product(smooth[i - 2], c3));
            error[i] = Round(Round(Product(Round(price - smooth[i]), c1) + Product(error[i - 1], c2)) + Product(error[i - 2], c3));
            result[i] = Round(smooth[i] + error[i]).ToDouble();
        }
        return result;
    }
}
