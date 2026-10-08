using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SeededHilbertTrendReference
{
    private static ReferenceFraction F(double value) => ReferenceFraction.FromDouble(value);

    internal static double?[][] Values(IReadOnlyList<Bar> bars, bool midpoint)
    {
        var prices = bars.Select(b =>
                midpoint ? ((F(b.High) + F(b.Low)) / F(2)).ToDouble() : b.Close
            )
            .ToArray();
        var input = bars.Select(
                (b, i) => new Bar(b.Time, prices[i], prices[i], prices[i], prices[i], b.Volume)
            )
            .ToArray();
        var period = SeededPhaseReference.Cycle(input, 0, 6, 6, false)[2];
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        var means = Enumerable.Repeat(F(0), bars.Count).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            result[0][i] = prices[i];
            if (i < 6)
                continue;
            var count = Math.Min((int)(period[i]!.Value + .5), i + 1);
            means[i] =
                count > 0
                    ? (
                        prices.Skip(i - count + 1).Take(count).Aggregate(F(0), (s, p) => s + F(p))
                        / F(count)
                    ).RoundExtendedBinary64()
                    : F(prices[i]);
            result[1][i] = (
                (
                    F(4) * F(prices[i])
                    + F(3) * F(prices[i - 1])
                    + F(2) * F(prices[i - 2])
                    + F(prices[i - 3])
                ) / F(10)
            ).ToDouble();
            if (count > 0)
                result[2][i] = count;
            if (i >= 11)
                result[0][i] = (
                    (F(4) * means[i] + F(3) * means[i - 1] + F(2) * means[i - 2] + means[i - 3])
                    / F(10)
                ).ToDouble();
        }
        return result;
    }
}
