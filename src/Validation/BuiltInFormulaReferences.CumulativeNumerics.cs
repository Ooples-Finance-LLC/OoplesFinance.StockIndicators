using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedCumulative(IReadOnlyList<Bar> bars, bool volume)
    {
        var result = new double[bars.Count];
        var sum = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            if (!volume) sum += ReferenceFraction.FromDouble(bars[i].Close);
            else if (i > 0)
            {
                var direction = (ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i - 1].Close)).Sign;
                sum += new ReferenceFraction(direction) * ReferenceFraction.FromDouble(bars[i].Volume);
            }
            result[i] = sum.ToDouble();
        }
        return result;
    }
}
