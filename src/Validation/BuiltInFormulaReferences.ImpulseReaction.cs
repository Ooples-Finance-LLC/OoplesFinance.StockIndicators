using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ImpulseReactionOutputs(IReadOnlyList<Bar> bars, int length1 = 2, int length2 = 20, double q = .9)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction a, ReferenceFraction b) => Round(a * b);
        var gain = ReferenceFraction.FromDouble(q);
        var c2 = Product(Product(gain, new ReferenceFraction(2)), ReferenceFraction.FromDouble(Math.Cos(2 * Math.PI / length2)));
        var c3 = Product(new ReferenceFraction(0) - gain, gain); var c1 = Product(Round(new ReferenceFraction(1) + c3), ReferenceFraction.FromDouble(.5));
        var history = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var prior = ReferenceFraction.FromDouble(i >= length1 ? bars[i - length1].Close : 0);
            var previous1 = i > 0 ? history[i - 1] : new ReferenceFraction(0); var previous2 = i > 1 ? history[i - 2] : new ReferenceFraction(0);
            history[i] = Round(Round(Product(c1, Round(price - prior)) + Product(c2, previous1)) + Product(c3, previous2));
            result[i] = price.Sign == 0 ? 0 : (Product(history[i], new ReferenceFraction(100)) / price).ToDouble();
        }
        return result;
    }
}
