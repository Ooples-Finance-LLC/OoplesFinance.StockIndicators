using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SvamaOutputs(IReadOnlyList<Bar> bars)
        => new Dictionary<string, double[]> { ["Svama"] = SvamaValues(bars).Line };
    internal static (double[] Line, Signal[] Signals) SvamaValues(IReadOnlyList<Bar> bars)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var values = new ReferenceFraction[bars.Count]; var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = R(bars[i].Close); var previous = i == 0 ? current : values[i - 1];
            var highest = R(bars.Take(i + 1).Max(b => b.Volume)); var volume = R(bars[i].Volume);
            // Independently group the two weighted terms before taking a single quotient.
            values[i] = highest.Sign == 0 ? previous : (volume * current + (highest - volume) * previous) / highest;
            var comparison = current - values[i]; var priorComparison = i == 0 ? zero : R(bars[i - 1].Close) - values[i - 1];
            trades[i] = comparison.Sign > 0 && comparison.CompareTo(priorComparison) > 0 ? Signal.StrongBuy
                : comparison.Sign < 0 && comparison.CompareTo(priorComparison) < 0 ? Signal.StrongSell
                : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (values.Select(v => v.ToDouble()).ToArray(), trades);
    }
}
