using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedAverageDayRange(IReadOnlyList<Bar> bars, int length)
        => bars.Select((_, i) => {
            if (i + 1 < length) return 0;
            var sum = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++)
                sum += ReferenceFraction.FromDouble(bars[j].High) - ReferenceFraction.FromDouble(bars[j].Low);
            return (sum / new ReferenceFraction(length)).ToDouble();
        }).ToArray();

    internal static double[] RoundedBarRange(IReadOnlyList<Bar> bars, bool trueRange)
        => bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High);
            var low = ReferenceFraction.FromDouble(b.Low);
            var range = high - low;
            if (trueRange && i > 0)
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                foreach (var candidate in new[] { (high - previous).Abs(), (low - previous).Abs() })
                    if (candidate.CompareTo(range) > 0) range = candidate;
            }
            return range.ToDouble();
        }).ToArray();
}
