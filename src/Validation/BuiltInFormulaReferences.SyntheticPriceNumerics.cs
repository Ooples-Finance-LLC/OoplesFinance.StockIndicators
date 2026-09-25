using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedSyntheticPriceReference(IReadOnlyList<Bar> bars, int length)
    {
        var gain = ReferenceFraction.FromDouble(length > 2 ? 2d / (length + 1d) : .67);
        var halfGain = gain / new ReferenceFraction(2);
        var one = new ReferenceFraction(1);
        var result = new double[bars.Count];
        var first = new ReferenceFraction(0);
        var second = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var high = Math.Max(bars[i].High, i == 0 ? 0 : bars[i - 1].High);
            var low = Math.Min(bars[i].Low, i == 0 ? 0 : bars[i - 1].Low);
            var price = ReferenceFraction.FromDouble(((ReferenceFraction.FromDouble(high) + ReferenceFraction.FromDouble(low)) / new ReferenceFraction(2)).ToDouble());
            if (i == 0) first = second = price;
            first = ReferenceFraction.FromDouble((gain * price + (one - gain) * first).ToDouble());
            second = ReferenceFraction.FromDouble((halfGain * price + (one - halfGain) * second).ToDouble());
            result[i] = (first - second).ToDouble();
        }
        return result;
    }
}
