using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedPercentageStops(IReadOnlyList<Bar> bars, object options)
    {
        var length = Integer(options, "Length", 100);
        var fraction = ReferenceFraction.FromDouble(Number(options, 10, "Pct")) / new ReferenceFraction(100);
        double Threshold(double price, int direction) => (ReferenceFraction.FromDouble(price) *
            (new ReferenceFraction(1) + new ReferenceFraction(direction) * fraction)).ToDouble();
        var longs = new double[bars.Count];
        var shorts = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = i == 0 ? bars[i].Close : Window(bars, i - 1, length).Max(b => b.High);
            var low = i == 0 ? bars[i].Close : Window(bars, i - 1, length).Min(b => b.Low);
            longs[i] = bars[i].High > high ? Threshold(bars[i].High, -1) : i == 0 ? bars[i].Close : longs[i - 1];
            shorts[i] = bars[i].Low < low ? Threshold(bars[i].Low, 1) : i == 0 ? bars[i].Close : shorts[i - 1];
        }
        return Outputs(("LongStop", longs), ("ShortStop", shorts));
    }
}
