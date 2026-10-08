using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] CyberCycleValues(IReadOnlyList<Bar> bars, double alpha = .07)
        => CyberCycleStages(bars, alpha).Select(v => v.ToDouble()).ToArray();
    private static ReferenceFraction[] CyberCycleStages(IReadOnlyList<Bar> bars, double alpha)
    {
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction a, ReferenceFraction b) => Round(a * b);
        var lead = ReferenceFraction.FromDouble(1 - .5 * alpha); var retention = ReferenceFraction.FromDouble(1 - alpha); var square = Product(retention, retention); lead = Product(lead, lead); retention = Product(retention, new ReferenceFraction(2));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var smooth = new ReferenceFraction[bars.Count]; var cycle = new ReferenceFraction[bars.Count];
        ReferenceFraction Previous(ReferenceFraction[] values, int i) => i >= 0 ? values[i] : new ReferenceFraction(0);
        ReferenceFraction Twice(ReferenceFraction v) => Product(v, new ReferenceFraction(2));
        ReferenceFraction Difference(ReferenceFraction[] values, int i) => Round(Round(values[i] - Twice(Previous(values, i - 1))) + Previous(values, i - 2));
        for (var i = 0; i < bars.Count; i++)
        {
            var total = Round(Round(Round(prices[i] + Twice(Previous(prices, i - 1))) + Twice(Previous(prices, i - 2))) + Previous(prices, i - 3)); smooth[i] = Round(total / new ReferenceFraction(6));
            cycle[i] = Round(Round(Product(lead, Difference(smooth, i)) + Product(retention, Previous(cycle, i - 1))) - Product(square, Previous(cycle, i - 2)));
            if (i < 7) cycle[i] = Round(Difference(prices, i) / new ReferenceFraction(4));
        }
        return cycle;
    }
}
