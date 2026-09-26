using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedMidpoint(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var line = bars.Select((bar, i) =>
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var high = ReferenceFraction.FromDouble(window.Max(b => b.High));
            var low = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            if (high.CompareTo(low) == 0) return 0d;
            var position = (ReferenceFraction.FromDouble(bar.Close) - low) / (high - low);
            return Math.Max(-100, Math.Min(100, (position * new ReferenceFraction(200) - new ReferenceFraction(100)).ToDouble()));
        }).ToArray();
        return Outputs(("Mo", line), ("Signal", RoundedBoundedStage(line, 9, kind)));
    }
}
