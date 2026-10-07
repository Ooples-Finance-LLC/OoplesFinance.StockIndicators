using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class DelayedHilbertTrendReference
{
    internal static double?[] Values(IReadOnlyList<Bar> bars, int suppression)
    {
        var result = new double?[bars.Count];
        if (63L + suppression >= bars.Count)
            return result;
        var periods = SeededPhaseReference.Cycle(bars, 0, 37, 37)[2];
        var means = Enumerable.Repeat(new ReferenceFraction(0), bars.Count).ToArray();
        for (var i = 37; i < bars.Count; i++)
        {
            var count = (int)(periods[i]!.Value + .5);
            if (count > 0)
            {
                var sum = bars.Skip(i - count + 1)
                    .Take(count)
                    .Aggregate(
                        new ReferenceFraction(0),
                        (s, b) => s + ReferenceFraction.FromDouble(b.Close)
                    );
                means[i] = (sum / new ReferenceFraction(count)).RoundExtendedBinary64();
            }
            if (i >= 63L + suppression)
                result[i] = (
                    (
                        new ReferenceFraction(4) * means[i]
                        + new ReferenceFraction(3) * means[i - 1]
                        + new ReferenceFraction(2) * means[i - 2]
                        + means[i - 3]
                    ) / new ReferenceFraction(10)
                ).ToDouble();
        }
        return result;
    }
}
