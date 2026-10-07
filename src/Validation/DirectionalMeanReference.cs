using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class DirectionalMeanReference
{
    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    internal static double?[][] Calculate(IReadOnlyList<Bar> bars, int period, int lag)
    {
        var result = Enumerable.Range(0, 7).Select(_ => new double?[bars.Count]).ToArray();
        var plus = new ReferenceFraction[bars.Count];
        var minus = new ReferenceFraction[bars.Count];
        var ranges = new ReferenceFraction[bars.Count];
        ReferenceFraction pmean = new(0),
            mmean = new(0),
            atr = new(0);
        for (var i = 0; i < bars.Count; i++)
        {
            result[4][i] = 0;
            if (i == 0)
                continue;
            var up = R(bars[i].High) - R(bars[i - 1].High);
            var down = R(bars[i - 1].Low) - R(bars[i].Low);
            result[0][i] = up.ToDouble();
            result[1][i] = down.ToDouble();
            plus[i] = (
                up.Sign > 0 && up.CompareTo(down) > 0 ? up : new ReferenceFraction(0)
            ).RoundExtendedBinary64();
            minus[i] = (
                down.Sign > 0 && down.CompareTo(up) > 0 ? down : new ReferenceFraction(0)
            ).RoundExtendedBinary64();
            var h = R(bars[i].High);
            var l = R(bars[i].Low);
            var c = R(bars[i - 1].Close);
            ranges[i] = new[] { h - l, (h - c).Abs(), (l - c).Abs() }.Max().RoundExtendedBinary64();
            if (i < period)
                continue;
            ReferenceFraction Mean(ReferenceFraction[] input, ReferenceFraction prior) =>
                (
                    i == period
                        ? input
                            .Skip(1)
                            .Take(period)
                            .Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                            / new ReferenceFraction(period)
                        : (prior * new ReferenceFraction(period - 1) + input[i])
                            / new ReferenceFraction(period)
                ).RoundExtendedBinary64();
            pmean = Mean(plus, pmean);
            mmean = Mean(minus, mmean);
            atr = Mean(ranges, atr);
            if (atr.Sign != 0)
            {
                var pdi = (new ReferenceFraction(100) * pmean / atr).RoundExtendedBinary64();
                var mdi = (new ReferenceFraction(100) * mmean / atr).RoundExtendedBinary64();
                result[2][i] = pdi.ToDouble();
                result[3][i] = mdi.ToDouble();
                result[4][i] =
                    (pdi + mdi).Sign == 0
                        ? null
                        : (new ReferenceFraction(100) * (pdi - mdi).Abs() / (pdi + mdi)).ToDouble();
            }
            if (i == period)
            {
                var seed = result[4]
                    .Skip(1)
                    .Take(period)
                    .Where(v => v.HasValue)
                    .Select(v => R(v!.Value))
                    .ToArray();
                result[5][i] =
                    seed.Length == 0
                        ? null
                        : (
                            seed.Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                            / new ReferenceFraction(seed.Length)
                        ).ToDouble();
            }
            else if (result[5][i - 1].HasValue && result[4][i].HasValue)
                result[5][i] = (
                    (
                        new ReferenceFraction(period - 1) * R(result[5][i - 1]!.Value)
                        + R(result[4][i]!.Value)
                    ) / new ReferenceFraction(period)
                ).ToDouble();
            if (i >= lag && result[5][i].HasValue && result[5][i - lag].HasValue)
                result[6][i] = (
                    (R(result[5][i]!.Value) + R(result[5][i - lag]!.Value))
                    / new ReferenceFraction(2)
                ).ToDouble();
        }
        return result;
    }
}
