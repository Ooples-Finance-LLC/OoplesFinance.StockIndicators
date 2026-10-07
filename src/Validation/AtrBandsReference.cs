using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class AtrBandsReference
{
    internal static double[][] Calculate(
        IReadOnlyList<Bar> bars,
        int centerPeriod,
        int atrPeriod,
        double multiplier,
        AtrBandCenterMode mode,
        bool width
    )
    {
        var result = Enumerable.Range(0, 8).Select(_ => new double[bars.Count]).ToArray();
        var seed = new ReferenceFraction(0);
        var atr = seed;
        var center = seed;
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            if (i > 0)
            {
                var h = ReferenceFraction.FromDouble(bars[i].High);
                var l = ReferenceFraction.FromDouble(bars[i].Low);
                var range = new[] { h - l, (h - prices[i - 1]).Abs(), (l - prices[i - 1]).Abs() }
                    .Max()
                    .RoundExtendedBinary64();
                if (i <= atrPeriod)
                    seed += range;
                if (i >= atrPeriod)
                    atr = (
                        i == atrPeriod
                            ? seed / new ReferenceFraction(atrPeriod)
                            : (atr * new ReferenceFraction(atrPeriod - 1) + range)
                                / new ReferenceFraction(atrPeriod)
                    ).RoundExtendedBinary64();
            }
            if (mode == AtrBandCenterMode.FirstSeededExponential)
                center =
                    i == 0
                        ? prices[0]
                        : ReferenceFraction.FromDouble(
                            (
                                (
                                    new ReferenceFraction(2) * prices[i]
                                    + new ReferenceFraction(centerPeriod - 1) * center
                                ) / new ReferenceFraction((long)centerPeriod + 1)
                            ).ToDouble()
                        );
            else if (i >= centerPeriod - 1)
                center = ReferenceFraction.FromDouble(
                    (
                        mode == AtrBandCenterMode.Simple || i == centerPeriod - 1
                            ? prices
                                .Skip(i - centerPeriod + 1)
                                .Take(centerPeriod)
                                .Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                                / new ReferenceFraction(centerPeriod)
                            : (
                                new ReferenceFraction(2) * prices[i]
                                + new ReferenceFraction(centerPeriod - 1) * center
                            ) / new ReferenceFraction((long)centerPeriod + 1)
                    ).ToDouble()
                );
            if (
                i
                < (
                    mode == AtrBandCenterMode.Simple
                        ? centerPeriod
                        : Math.Max(centerPeriod, atrPeriod)
                ) - 1
            )
                continue;
            result[0][i] = center.ToDouble();
            result[4][i] = 1;
            if (i < atrPeriod)
                continue;
            var offset = atr * ReferenceFraction.FromDouble(multiplier);
            result[1][i] = (center + offset).ToDouble();
            result[2][i] = (center - offset).ToDouble();
            result[5][i] = result[6][i] = 1;
            if (
                !width
                || center.Sign == 0
                || !double.IsFinite(result[1][i])
                || !double.IsFinite(result[2][i])
            )
                continue;
            result[3][i] = (
                (
                    ReferenceFraction.FromDouble(result[1][i])
                    - ReferenceFraction.FromDouble(result[2][i])
                ) / center
            ).ToDouble();
            result[7][i] = 1;
        }
        return result;
    }
}
