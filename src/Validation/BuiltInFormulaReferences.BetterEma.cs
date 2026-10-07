using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BetterEmaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 20)); var angle = 2 * Math.PI / length;
        var value = Math.Sin(angle) + Math.Cos(angle); var gain = value == 0 ? .01 : Math.Max(.01, Math.Min(.99, (value - 1) / value));
        var alpha = ReferenceFraction.FromDouble(gain); var retention = new ReferenceFraction(1) - alpha;
        var previous = new ReferenceFraction(0); var ema = new ReferenceFraction(0); var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var midpoint = ReferenceFraction.FromDouble(((price + previous) / new ReferenceFraction(2)).ToDouble());
            output[i] = (alpha * midpoint + retention * ema).ToDouble();
            ema = ReferenceFraction.FromDouble((alpha * price + retention * ema).ToDouble()); previous = price;
        }
        return Outputs(("Ebema", output));
    }
}
