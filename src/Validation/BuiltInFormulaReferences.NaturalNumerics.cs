using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedNaturalMean(IReadOnlyList<Bar> bars, int length)
    {
        var logs = bars.Select(b => b.Close > 0 ? Math.Log(b.Close) * 1000 : 0).ToArray();
        var changes = logs.Select((value, i) => Math.Abs(value - (i == 0 ? 0 : logs[i - 1]))).ToArray();
        return bars.Select((bar, i) =>
        {
            var terms = Enumerable.Range(0, Math.Min(length, i + 1)).Select(lag =>
                (Movement: ReferenceFraction.FromDouble(changes[i - lag]),
                 Tap: ReferenceFraction.FromDouble(Math.Sqrt(lag + 1d) - Math.Sqrt(lag)))).ToArray();
            var total = terms.Aggregate(new ReferenceFraction(0), (sum, t) => sum + t.Movement);
            var weighted = terms.Aggregate(new ReferenceFraction(0), (sum, t) => sum + t.Movement * t.Tap);
            var ratio = ReferenceFraction.FromDouble(total.Sign == 0 ? 0 : (weighted / total).ToDouble());
            var previous = ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Close);
            return (previous * (new ReferenceFraction(1) - ratio) + ReferenceFraction.FromDouble(bar.Close) * ratio).ToDouble();
        }).ToArray();
    }
}
