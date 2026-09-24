using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedTrailingReverse(IReadOnlyList<Bar> bars, int length)
    {
        var fraction = new ReferenceFraction(Math.Max(1, length)) / new ReferenceFraction(100);
        var line = new double[bars.Count];
        var rising = true;
        var extreme = 0d;
        double Threshold() => (ReferenceFraction.FromDouble(extreme) *
            (new ReferenceFraction(1) + new ReferenceFraction(rising ? -1 : 1) * fraction)).ToDouble();
        for (var i = 0; i < bars.Count; i++)
        {
            var price = bars[i].Close;
            extreme = rising ? Math.Max(extreme, price) : Math.Min(extreme, price);
            var threshold = Threshold();
            if (rising ? price <= threshold : price > threshold)
            {
                rising = !rising;
                extreme = price;
            }
            line[i] = Threshold();
        }
        return line;
    }
}
