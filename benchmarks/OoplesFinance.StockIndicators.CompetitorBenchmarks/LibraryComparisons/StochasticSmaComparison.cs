using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis.Infrastructure;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class StochasticSmaComparison
{
    internal static readonly string[] Variants =
    [
        "Raw",
        "Fast",
        "Slow",
        "Full",
        "FastDifference",
        "SlowDifference",
        "FullDifference",
    ];
    internal static readonly ComparisonPair[] Pairs = Variants.Select(v => Create(v)).ToArray();

    internal static string Id(string variant) =>
        variant == "Raw"
            ? "Trady.Indicator.RawStochasticsValue"
            : "Trady.Indicator.Stochastics"
                + (Difference(variant) ? "Oscillator" : "")
                + "+"
                + variant.Replace("Difference", "");

    internal static bool Difference(string variant) =>
        variant.EndsWith("Difference", StringComparison.Ordinal);

    internal static int KPeriod(string variant, int k) =>
        variant.StartsWith("Fast", StringComparison.Ordinal) ? 1
        : variant.StartsWith("Slow", StringComparison.Ordinal) ? 3
        : k;

    internal static string[] Names(string variant) =>
        variant == "Raw" || Difference(variant) ? ["Value"] : ["K", "D", "J"];

    internal static ComparisonPair Create(string variant, int k = 3, int d = 3) =>
        new(
            Id(variant),
            variant == "Raw" ? nameof(FiftySeedStochastic)
                : Difference(variant) ? nameof(StochasticSmaDifference)
                : nameof(StochasticSmaKdj),
            (data, p) => Native(data, p, variant, k, d),
            (data, p) => Owned(data.IndicatorBars, p, variant, k, d),
            (data, p) => Reference(data, p, variant, k, d, false),
            Names(variant),
            CompetitorReference: (data, p) => Reference(data, p, variant, k, d, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static IIndicator Indicator(int p, string variant, int k, int d) =>
        variant == "Raw" ? new FiftySeedStochastic(p)
        : Difference(variant) ? new StochasticSmaDifference(p, KPeriod(variant, k), d)
        : new StochasticSmaKdj(p, KPeriod(variant, k), d);

    internal static ComparisonSeries Owned(Bar[] bars, int p, string variant, int k, int d)
    {
        var indicator = Indicator(p, variant, k, d);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var count = Names(variant).Length;
        return RetrospectivePriceComparison.Series(
            Names(variant),
            Enumerable
                .Range(0, count)
                .Select(slot =>
                {
                    var present =
                        variant == "Raw"
                            ? Enumerable.Repeat(1d, bars.Length).ToArray()
                            : run[indicator.Outputs[slot + count]].ToArray();
                    return run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => present[i] > 0 ? (double?)v : null)
                        .ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(CompetitorData data, int p, string variant, int k, int d)
    {
        if (variant == "Raw")
            return VolumePriceComparison.Mask(
                new T.RawStochasticsValue(data.Candles, p)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
            );
        if (Difference(variant))
        {
            var values = variant switch
            {
                "FastDifference" => new T.StochasticsOscillator.Fast(data.Candles, p, d)
                    .Compute()
                    .Select(r => (double?)r.Tick),
                "SlowDifference" => new T.StochasticsOscillator.Slow(data.Candles, p, d)
                    .Compute()
                    .Select(r => (double?)r.Tick),
                _ => new T.StochasticsOscillator.Full(data.Candles, p, k, d)
                    .Compute()
                    .Select(r => (double?)r.Tick),
            };
            return VolumePriceComparison.Mask(values.ToArray());
        }
        var rows = variant switch
        {
            "Fast" => new T.Stochastics.Fast(data.Candles, p, d)
                .Compute()
                .Select(r => r.Tick)
                .ToArray(),
            "Slow" => new T.Stochastics.Slow(data.Candles, p, d)
                .Compute()
                .Select(r => r.Tick)
                .ToArray(),
            _ => new T.Stochastics.Full(data.Candles, p, k, d)
                .Compute()
                .Select(r => r.Tick)
                .ToArray(),
        };
        return RetrospectivePriceComparison.Series(
            Names(variant),
            [
                rows.Select(r => (double?)r.K).ToArray(),
                rows.Select(r => (double?)r.D).ToArray(),
                rows.Select(r => (double?)r.J).ToArray(),
            ]
        );
    }

    internal static AnalyzableBase<
        (decimal High, decimal Low, decimal Close),
        (decimal High, decimal Low, decimal Close),
        decimal?,
        decimal?
    > ScalarTuple(
        (decimal High, decimal Low, decimal Close)[] values,
        int p,
        string variant,
        int k,
        int d
    ) =>
        variant switch
        {
            "Raw" => new T.RawStochasticsValueByTuple(values, p),
            "FastDifference" => new T.StochasticsOscillator.FastByTuple(values, p, d),
            "SlowDifference" => new T.StochasticsOscillator.SlowByTuple(values, p, d),
            _ => new T.StochasticsOscillator.FullByTuple(values, p, k, d),
        };

    internal static AnalyzableBase<
        (decimal High, decimal Low, decimal Close),
        (decimal High, decimal Low, decimal Close),
        (decimal? K, decimal? D, decimal? J),
        (decimal? K, decimal? D, decimal? J)
    > Tuple(
        (decimal High, decimal Low, decimal Close)[] values,
        int p,
        string variant,
        int k,
        int d
    ) =>
        variant switch
        {
            "Fast" => new T.Stochastics.FastByTuple(values, p, d),
            "Slow" => new T.Stochastics.SlowByTuple(values, p, d),
            _ => new T.Stochastics.FullByTuple(values, p, k, d),
        };

    internal static ComparisonSeries Reference(
        CompetitorData data,
        int p,
        string variant,
        int k,
        int d,
        bool native
    ) =>
        native
            ? RetrospectivePriceComparison.Series(
                Names(variant),
                NativeReference(
                        data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray(),
                        p,
                        variant,
                        k,
                        d
                    )
                    .Select(row => row.Select(v => (double?)v).ToArray())
                    .ToArray()
            )
            : RetrospectivePriceComparison.Series(
                Names(variant),
                OwnedReference(data.IndicatorBars, p, variant, k, d)
            );

    internal static double?[][] OwnedReference(Bar[] bars, int p, string variant, int k, int d)
    {
        BigInteger Stage(BigInteger n, BigInteger denominator)
        {
            var shift = 0;
            double rounded;
            while (!double.IsFinite(rounded = Round(n, denominator << shift)))
                shift += 512;
            return Units(rounded) << shift;
        }
        var raw = Enumerable.Repeat<BigInteger?>(50 * Grid, bars.Length).ToArray();
        for (var i = p - 1; i < bars.Length; i++)
        {
            var hi = bars.Skip(i - p + 1).Take(p).Max(b => b.High);
            var lo = bars.Skip(i - p + 1).Take(p).Min(b => b.Low);
            if (hi != lo) // NOSONAR: Exact equality defines the flat-range branch.
                raw[i] = Stage(100 * (Units(bars[i].Close) - Units(lo)), Units(hi) - Units(lo));
        }
        double?[] Publish(BigInteger?[] values) =>
            values.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray();
        if (variant == "Raw")
            return [Publish(raw)];
        BigInteger?[] Mean(BigInteger?[] values, int length)
        {
            var means = new BigInteger?[values.Length];
            for (var i = length - 1; i < values.Length; i++)
            {
                var known = values
                    .Skip(i - length + 1)
                    .Take(length)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToArray();
                if (known.Length > 0)
                    means[i] = Stage(
                        known.Aggregate(BigInteger.Zero, (sum, v) => sum + v),
                        known.Length * Grid
                    );
            }
            return means;
        }
        var a = Mean(raw, KPeriod(variant, k));
        var b = Mean(a, d);
        var combined = a.Select(
                (v, i) =>
                    v.HasValue && b[i].HasValue
                        ? (double?)Round(
                            (Difference(variant) ? 1 : 3) * v.Value
                                - (Difference(variant) ? 1 : 2) * b[i]!.Value,
                            Grid
                        )
                        : null
            )
            .ToArray();
        return Difference(variant) ? [combined] : [Publish(a), Publish(b), combined];
    }

    internal static decimal?[][] NativeReference(
        (decimal High, decimal Low, decimal Close)[] prices,
        int p,
        string variant,
        int k,
        int d
    )
    {
        var raw = Enumerable.Repeat<decimal?>(50, prices.Length).ToArray();
        for (var i = p - 1; i < prices.Length; i++)
        {
            var hi = prices.Skip(i - p + 1).Take(p).Max(b => b.High);
            var lo = prices.Skip(i - p + 1).Take(p).Min(b => b.Low);
            if (hi != lo)
                raw[i] = 100 * (prices[i].Close - lo) / (hi - lo);
        }
        if (variant == "Raw")
            return [raw];
        decimal?[] Mean(decimal?[] values, int length)
        {
            var means = new decimal?[values.Length];
            for (var i = length - 1; i < values.Length; i++)
            {
                decimal sum = 0;
                var count = 0;
                for (var j = i - length + 1; j <= i; j++)
                    if (values[j] is { } value)
                    {
                        sum += value;
                        count++;
                    }
                if (count > 0)
                    means[i] = sum / count;
            }
            return means;
        }
        var a = Mean(raw, KPeriod(variant, k));
        var b = Mean(a, d);
        var combined = a.Select((v, i) => Difference(variant) ? v - b[i] : 3 * v - 2 * b[i])
            .ToArray();
        return Difference(variant) ? [combined] : [a, b, combined];
    }
}
