using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis.Infrastructure;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class NullableStrengthComparison
{
    internal static bool Momentum(NullableStrengthConvention c) =>
        c
            is NullableStrengthConvention.RelativeMomentum
                or NullableStrengthConvention.RelativeMomentumIndex;

    internal static readonly ComparisonPair[] Pairs = Enum.GetValues<NullableStrengthConvention>()
        .Select(c => Create(c, Momentum(c) ? 5 : 1))
        .ToArray();

    internal static string Name(NullableStrengthConvention c) =>
        c == NullableStrengthConvention.NetMomentum ? "NetMomentumOscillator" : c.ToString();

    internal static ComparisonPair Create(NullableStrengthConvention c, int lag = 1) =>
        new(
            "Trady.Indicator." + Name(c),
            nameof(NullableStrengthOscillator),
            (d, p) => Native(d, p, c, lag),
            (d, p) => Owned(d.IndicatorBars, p, c, lag),
            (d, p) =>
                VolumePriceComparison.Mask(
                    OwnedReference(d.Closes.Select(x => (double?)x).ToArray(), p, c, lag)
                ),
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    NativeReference(d.Candles.Select(x => (decimal?)x.Close).ToArray(), p, c, lag)
                        .Select(x => (double?)x)
                        .ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData d,
        int p,
        NullableStrengthConvention c,
        int lag
    )
    {
        var values = c switch
        {
            NullableStrengthConvention.RelativeStrength => new T.RelativeStrength(d.Candles, p)
                .Compute()
                .Select(r => (double?)r.Tick),
            NullableStrengthConvention.RelativeStrengthIndex => new T.RelativeStrengthIndex(
                d.Candles,
                p
            )
                .Compute()
                .Select(r => (double?)r.Tick),
            NullableStrengthConvention.NetMomentum => new T.NetMomentumOscillator(d.Candles, p)
                .Compute()
                .Select(r => (double?)r.Tick),
            NullableStrengthConvention.RelativeMomentum => new T.RelativeMomentum(d.Candles, p, lag)
                .Compute()
                .Select(r => (double?)r.Tick),
            _ => new T.RelativeMomentumIndex(d.Candles, p, lag)
                .Compute()
                .Select(r => (double?)r.Tick),
        };
        return VolumePriceComparison.Mask(values.ToArray());
    }

    internal static AnalyzableBase<decimal?, decimal?, decimal?, decimal?> Tuple(
        decimal?[] input,
        int p,
        NullableStrengthConvention c,
        int lag
    ) =>
        c switch
        {
            NullableStrengthConvention.RelativeStrength => new T.RelativeStrengthByTuple(input, p),
            NullableStrengthConvention.RelativeStrengthIndex => new T.RelativeStrengthIndexByTuple(
                input,
                p
            ),
            NullableStrengthConvention.NetMomentum => new T.NetMomentumOscillatorByTuple(input, p),
            NullableStrengthConvention.RelativeMomentum => new T.RelativeMomentumByTuple(
                input,
                p,
                lag
            ),
            _ => new T.RelativeMomentumIndexByTuple(input, p, lag),
        };

    internal static AnalyzableBase<int, decimal?, decimal?, decimal?> Generic(
        decimal?[] input,
        int p,
        NullableStrengthConvention c,
        int lag
    ) =>
        c switch
        {
            NullableStrengthConvention.RelativeStrength => new T.RelativeStrength<int, decimal?>(
                Enumerable.Range(0, input.Length),
                i => input[i],
                p
            ),
            NullableStrengthConvention.RelativeStrengthIndex => new T.RelativeStrengthIndex<
                int,
                decimal?
            >(Enumerable.Range(0, input.Length), i => input[i], p),
            NullableStrengthConvention.NetMomentum => new T.NetMomentumOscillator<int, decimal?>(
                Enumerable.Range(0, input.Length),
                i => input[i],
                p
            ),
            NullableStrengthConvention.RelativeMomentum => new T.RelativeMomentum<int, decimal?>(
                Enumerable.Range(0, input.Length),
                i => input[i],
                p,
                lag
            ),
            _ => new T.RelativeMomentumIndex<int, decimal?>(
                Enumerable.Range(0, input.Length),
                i => input[i],
                p,
                lag
            ),
        };

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int p,
        NullableStrengthConvention c,
        int lag
    )
    {
        var indicator = new NullableStrengthOscillator(p, c, lag);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            run[indicator.Value]
                .ToArray()
                .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                .ToArray()
        );
    }

    internal static decimal?[] NativeReference(
        decimal?[] input,
        int p,
        NullableStrengthConvention c,
        int lag
    )
    {
        var result = new decimal?[input.Length];
        var first = (long)p + lag - 1;
        if (first >= input.Length)
            return result;
        var changes = input.Select((value, i) => i < lag ? null : value - input[i - lag]).ToArray();
        var gains = changes
            .Select(d => d.HasValue ? (decimal?)Math.Max(d.Value, 0) : null)
            .ToArray();
        var losses = changes
            .Select(d => d.HasValue ? (decimal?)Math.Max(-d.Value, 0) : null)
            .ToArray();
        var up = gains.Skip(lag).Take(p).Average();
        var down = losses.Skip(lag).Take(p).Average();
        var alpha = Momentum(c) ? 2m / ((long)p + 1) : 1m / p;
        for (var i = (int)first; i < input.Length; i++)
        {
            if (i > first)
            {
                up = up + alpha * (gains[i] - up);
                down = down + alpha * (losses[i] - down);
            }
            var ratio = down.HasValue && down != 0 ? up / down : null;
            result[i] = c switch
            {
                NullableStrengthConvention.RelativeStrength
                or NullableStrengthConvention.RelativeMomentum => ratio,
                NullableStrengthConvention.RelativeStrengthIndex => 100 - 100 / (1 + ratio),
                NullableStrengthConvention.NetMomentum => 2 * (100 - 100 / (1 + ratio)) - 100,
                _ => ratio == 0 ? null : 100 * ratio / (1 + ratio),
            };
        }
        return result;
    }

    internal static double?[] OwnedReference(
        double?[] input,
        int p,
        NullableStrengthConvention c,
        int lag
    )
    {
        var result = new double?[input.Length];
        var first = (long)p + lag - 1;
        if (first >= input.Length)
            return result;
        var changes = input
            .Select(
                (x, i) =>
                    i >= lag && x.HasValue && input[i - lag].HasValue
                        ? (BigInteger?)(Units(x.Value) - Units(input[i - lag]!.Value))
                        : null
            )
            .ToArray();
        var seed = changes.Skip(lag).Take(p).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (seed.Length == 0)
            return result;
        var up = WilderStrengthComparison.Stage(
            seed.Aggregate(BigInteger.Zero, (sum, x) => sum + BigInteger.Max(x, 0)),
            seed.Length * Grid
        );
        var down = WilderStrengthComparison.Stage(
            seed.Aggregate(BigInteger.Zero, (sum, x) => sum + BigInteger.Max(-x, 0)),
            seed.Length * Grid
        );
        for (var i = (int)first; i < input.Length; i++)
        {
            if (i > first)
            {
                if (!changes[i].HasValue)
                    break;
                var change = changes[i]!.Value;
                var currentWeight = Momentum(c) ? 2 : 1;
                var mass = Momentum(c) ? (long)p + 1 : p;
                up = WilderStrengthComparison.Stage(
                    up.N * (p - 1) * Grid + currentWeight * BigInteger.Max(change, 0) * up.D,
                    up.D * Grid * mass
                );
                down = WilderStrengthComparison.Stage(
                    down.N * (p - 1) * Grid + currentWeight * BigInteger.Max(-change, 0) * down.D,
                    down.D * Grid * mass
                );
            }
            if (
                down.N.IsZero
                || c == NullableStrengthConvention.RelativeMomentumIndex && up.N.IsZero
            )
                continue;
            var upper = up.N * down.D;
            var lower = down.N * up.D;
            result[i] = c switch
            {
                NullableStrengthConvention.RelativeStrength
                or NullableStrengthConvention.RelativeMomentum => Round(upper, lower),
                NullableStrengthConvention.NetMomentum => Round(
                    100 * (upper - lower),
                    upper + lower
                ),
                _ => Round(100 * upper, upper + lower),
            };
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([
            5,
            4,
            7,
            2,
            2,
            5,
            1,
            3,
            4,
            1,
            1,
            6,
            2,
            0,
            1,
            4,
            3,
            3,
            2,
            5,
            1,
            6,
            0,
        ]);
}
