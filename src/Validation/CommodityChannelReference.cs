using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class CommodityChannelReference
{
    internal static double?[] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        bool rolling,
        bool flatZero
    )
    {
        var prices = bars.Select(b =>
                ReferenceFraction.FromDouble(
                    (
                        (
                            ReferenceFraction.FromDouble(b.High)
                            + ReferenceFraction.FromDouble(b.Low)
                            + ReferenceFraction.FromDouble(b.Close)
                        ) / new ReferenceFraction(3)
                    ).ToDouble()
                )
            )
            .ToArray();
        var means = new ReferenceFraction[prices.Length];
        var result = new double?[prices.Length];
        for (var i = period - 1; i < prices.Length; i++)
        {
            var mean =
                prices
                    .Skip(i - period + 1)
                    .Take(period)
                    .Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                / new ReferenceFraction(period);
            means[i] = ReferenceFraction.FromDouble(mean.ToDouble());
            if (rolling && (long)i < 2L * (period - 1))
                continue;
            var deviation = new ReferenceFraction(0);
            for (var j = i - period + 1; j <= i; j++)
            {
                var distance = prices[j] - (rolling ? means[j] : means[i]);
                deviation += distance.Sign < 0 ? new ReferenceFraction(-1) * distance : distance;
            }
            result[i] =
                deviation.Sign == 0
                    ? flatZero
                        ? 0
                        : null
                    : (
                        new ReferenceFraction(200L * period)
                        * (prices[i] - means[i])
                        / (new ReferenceFraction(3) * deviation)
                    ).ToDouble();
        }
        return result;
    }
}
