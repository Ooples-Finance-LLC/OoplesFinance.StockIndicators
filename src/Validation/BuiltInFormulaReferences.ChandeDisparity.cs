using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ChandeDisparityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => ChandeDisparityValues(bars, 200, 50, 20, 3).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ChandeDisparityValues(IReadOnlyList<Bar> bars, int length1, int length2, int length3, int kind, double[][]? external = null)
    {
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var prices = bars.Select(b => R(b.Close)).ToArray(); var periods = new[] { length1, length2, length3 };
        var means = periods.Select((n, j) => external is null ? SmoothRocBankStage(prices, Math.Max(1, n), kind) : external[j].Select(R).ToArray()).ToArray();
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            // Sum the three exact fractions independently; avoid forming any
            // binary64 leg or a rounded mean-of-means before normalization.
            var current = prices[i].Sign == 0 ? R(0) : Enumerable.Range(0, 3).Aggregate(R(0), (a, j) => a + R(100) * (prices[i] - means[j][i]) / prices[i]) / R(3);
            current = current.RoundExtendedBinary64(); values[i] = current.ToDouble(); var change = current - previous;
            signals[i] = current.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : current.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : current.Sign > 0 ? Signal.Buy : current.Sign < 0 ? Signal.Sell : Signal.None; previous = current;
        }
        return (new Dictionary<string, double[]> { ["Cmoadi"] = values }, signals);
    }
}
