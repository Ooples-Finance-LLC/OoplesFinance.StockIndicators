using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class TillsonReference
{
    internal static double?[] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        double factor,
        TillsonSeed seed,
        int suppression
    ) =>
        CalculateExtended(bars, period, factor, seed, suppression)
            .Select(v => v?.ToDouble())
            .ToArray();

    internal static ReferenceFraction?[] CalculateExtended(
        IReadOnlyList<Bar> bars,
        int period,
        double factor,
        TillsonSeed seed,
        int suppression
    ) =>
        CalculateExtended(
            bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(),
            period,
            factor,
            seed,
            suppression
        );

    internal static ReferenceFraction?[] CalculateExtended(
        IReadOnlyList<ReferenceFraction> prices,
        int period,
        double factor,
        TillsonSeed seed,
        int suppression
    )
    {
        var levels = Enumerable
            .Range(0, 6)
            .Select(_ => new ReferenceFraction?[prices.Count])
            .ToArray();
        var weight = new ReferenceFraction(2) / new ReferenceFraction((long)period + 1);
        for (var stage = 0; stage < 6; stage++)
        for (var i = 0; i < prices.Count; i++)
        {
            ReferenceFraction? Input(int at) => stage == 0 ? prices[at] : levels[stage - 1][at];
            var current = Input(i);
            if (!current.HasValue)
                continue;
            var start = seed == TillsonSeed.CascadedMeans ? (long)stage * (period - 1) : 0;
            if (seed == TillsonSeed.CascadedMeans && i < start + period - 1)
                continue;
            if (seed == TillsonSeed.CascadedMeans && i == start + period - 1)
                levels[stage][i] = (
                    Enumerable
                        .Range((int)start, period)
                        .Aggregate(new ReferenceFraction(0), (sum, j) => sum + Input(j)!.Value)
                    / new ReferenceFraction(period)
                ).RoundExtendedBinary64();
            else if (i == 0)
                levels[stage][i] = current;
            else if (seed == TillsonSeed.FollowingPrefix && i < period)
                levels[stage][i] = (
                    Enumerable
                        .Range(1, i)
                        .Aggregate(new ReferenceFraction(0), (sum, j) => sum + Input(j)!.Value)
                    / new ReferenceFraction(i)
                ).RoundExtendedBinary64();
            else
                levels[stage][i] = (
                    levels[stage][i - 1]!.Value * (new ReferenceFraction(1) - weight)
                    + current.Value * weight
                ).RoundExtendedBinary64();
        }
        var a = ReferenceFraction.FromDouble(factor);
        var a2 = a * a;
        var a3 = a2 * a;
        var coefficients = new[]
        {
            new ReferenceFraction(1)
                + new ReferenceFraction(3) * a
                + new ReferenceFraction(3) * a2
                + a3,
            new ReferenceFraction(-3) * a
                - new ReferenceFraction(6) * a2
                - new ReferenceFraction(3) * a3,
            new ReferenceFraction(3) * a2 + new ReferenceFraction(3) * a3,
            new ReferenceFraction(0) - a3,
        };
        var output = new ReferenceFraction?[prices.Count];
        var first = (seed == TillsonSeed.CascadedMeans ? 6L * (period - 1) : 0) + suppression;
        for (var i = 0; i < prices.Count; i++)
            if (i >= first)
                output[i] = Enumerable
                    .Range(0, 4)
                    .Aggregate(
                        new ReferenceFraction(0),
                        (sum, j) => sum + coefficients[j] * levels[j + 2][i]!.Value
                    )
                    .RoundExtendedBinary64();
        return output;
    }
}
