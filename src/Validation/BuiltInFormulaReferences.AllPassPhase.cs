using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] AllPassPhaseOutputs(IReadOnlyList<Bar> bars, int length = 20, double q = .5)
    {
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction a, ReferenceFraction b) => Round(a * b);
        var gain = ReferenceFraction.FromDouble(q); var cosine = length != 0 ? Math.Cos(2 * Math.PI / length) : 0;
        var a2 = q != 0 && length != 0 ? Round(ReferenceFraction.FromDouble(-2 * cosine) / gain) : new ReferenceFraction(0);
        var reciprocal = q != 0 ? Round(new ReferenceFraction(1) / gain) : new ReferenceFraction(0); var a3 = Product(reciprocal, reciprocal);
        var b2 = length != 0 ? Product(Product(gain, new ReferenceFraction(-2)), ReferenceFraction.FromDouble(cosine)) : new ReferenceFraction(0); var b3 = Product(gain, gain);
        var history = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var price1 = ReferenceFraction.FromDouble(i > 0 ? bars[i - 1].Close : 0); var price2 = ReferenceFraction.FromDouble(i > 1 ? bars[i - 2].Close : 0);
            var phase1 = i > 0 ? history[i - 1] : new ReferenceFraction(0); var phase2 = i > 1 ? history[i - 2] : new ReferenceFraction(0);
            var input = Round(Round(price + Product(a2, price1)) + Product(a3, price2));
            history[i] = Round(Round(Product(b3, input) - Product(b2, phase1)) - Product(b3, phase2)); result[i] = history[i].ToDouble();
        }
        return result;
    }
}
