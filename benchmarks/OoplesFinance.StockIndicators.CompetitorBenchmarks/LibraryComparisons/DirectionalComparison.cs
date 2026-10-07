using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DirectionalComparison
{
    internal static readonly string[] Names =
    [
        "PlusDirectionalMovement",
        "MinusDirectionalMovement",
        "PlusDirectionalIndicator",
        "MinusDirectionalIndicator",
        "DirectionalMovementIndex",
        "AverageDirectionalIndex",
        "AverageDirectionalIndexRating",
    ];
    internal static readonly ComparisonPair[] Pairs = Enumerable
        .Range(0, 7)
        .Select(i => Pair((DirectionalWindowMeasure)i))
        .ToArray();

    internal static ComparisonPair Pair(DirectionalWindowMeasure measure, int? lag = null) =>
        new(
            "Trady.Indicator." + Names[(int)measure],
            nameof(WindowDirectionalMeasure),
            (d, p) =>
                VolumePriceComparison.Mask(
                    Native(d, p, lag ?? p, measure).Select(v => (double?)v).ToArray()
                ),
            (d, p) => Owned(d.IndicatorBars, p, lag ?? p, measure),
            (d, p) =>
                VolumePriceComparison.Mask(Reference(d.IndicatorBars, p, lag ?? p)[(int)measure]),
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    DecimalReference(d, p, lag ?? p)[(int)measure].Select(v => (double?)v).ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static decimal?[] Native(
        CompetitorData d,
        int p,
        int lag,
        DirectionalWindowMeasure measure
    ) =>
        measure switch
        {
            DirectionalWindowMeasure.PositiveMovement => new T.PlusDirectionalMovement(d.Candles)
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            DirectionalWindowMeasure.NegativeMovement => new T.MinusDirectionalMovement(d.Candles)
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            DirectionalWindowMeasure.PositiveIndicator => new T.PlusDirectionalIndicator(
                d.Candles,
                p
            )
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            DirectionalWindowMeasure.NegativeIndicator => new T.MinusDirectionalIndicator(
                d.Candles,
                p
            )
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            DirectionalWindowMeasure.Index => new T.DirectionalMovementIndex(d.Candles, p)
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            DirectionalWindowMeasure.EarlyAverage => new T.AverageDirectionalIndex(d.Candles, p)
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
            _ => new T.AverageDirectionalIndexRating(d.Candles, p, lag)
                .Compute()
                .Select(v => v.Tick)
                .ToArray(),
        };

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        int lag,
        DirectionalWindowMeasure measure
    )
    {
        var indicator = new WindowDirectionalMeasure(measure, p, lag);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    // Integer-grid oracle: rebuild each initial window independently, then round
    // the specified stages. Extended units retain finite ratios of huge ranges.
    internal static BigInteger RoundedUnits(BigInteger n, BigInteger d)
    {
        if (n.IsZero)
            return 0;
        var negative = n.Sign < 0;
        n = BigInteger.Abs(n);
        var exponent = (int)(n.GetBitLength() - d.GetBitLength());
        if (exponent >= 0 ? n < (d << exponent) : (n << -exponent) < d)
            exponent--;
        var shift = Math.Max(0, exponent - 52);
        var divisor = d << shift;
        var q = BigInteger.DivRem(n, divisor, out var remainder);
        if (remainder * 2 > divisor || remainder * 2 == divisor && !q.IsEven)
            q++;
        return (negative ? -q : q) << shift;
    }

    internal static double?[][] Reference(Bar[] bars, int p, int lag)
    {
        var r = Enumerable.Range(0, 7).Select(_ => new double?[bars.Length]).ToArray();
        var movements = new[]
        {
            new BigInteger[bars.Length],
            new BigInteger[bars.Length],
            new BigInteger[bars.Length],
        };
        var means = new BigInteger[3];
        for (var i = 0; i < bars.Length; i++)
        {
            r[4][i] = 0;
            if (i == 0)
                continue;
            var up = Units(bars[i].High) - Units(bars[i - 1].High);
            var down = Units(bars[i - 1].Low) - Units(bars[i].Low);
            r[0][i] = Round(up, Grid);
            r[1][i] = Round(down, Grid);
            movements[0][i] = RoundedUnits(up > 0 && up > down ? up : 0, 1);
            movements[1][i] = RoundedUnits(down > 0 && down > up ? down : 0, 1);
            movements[2][i] = RoundedUnits(
                new[]
                {
                    Units(bars[i].High) - Units(bars[i].Low),
                    BigInteger.Abs(Units(bars[i].High) - Units(bars[i - 1].Close)),
                    BigInteger.Abs(Units(bars[i].Low) - Units(bars[i - 1].Close)),
                }.Max(),
                1
            );
            if (i < p)
                continue;
            for (var j = 0; j < 3; j++)
                means[j] = RoundedUnits(
                    i == p
                        ? movements[j].Skip(1).Take(p).Aggregate(BigInteger.Zero, (a, b) => a + b)
                        : means[j] * (p - 1) + movements[j][i],
                    p
                );
            if (!means[2].IsZero)
            {
                var plus = RoundedUnits(100 * means[0] * Grid, means[2]);
                var minus = RoundedUnits(100 * means[1] * Grid, means[2]);
                r[2][i] = Round(plus, Grid);
                r[3][i] = Round(minus, Grid);
                r[4][i] = (plus + minus).IsZero
                    ? null
                    : Round(100 * BigInteger.Abs(plus - minus), plus + minus);
            }
            if (i == p)
            {
                var seed = r[4]
                    .Skip(1)
                    .Take(p)
                    .Where(v => v.HasValue)
                    .Select(v => Units(v!.Value))
                    .ToArray();
                r[5][i] =
                    seed.Length == 0
                        ? null
                        : Round(
                            seed.Aggregate(BigInteger.Zero, (a, b) => a + b),
                            Grid * seed.Length
                        );
            }
            else if (r[5][i - 1].HasValue && r[4][i].HasValue)
                r[5][i] = Round(
                    Units(r[5][i - 1]!.Value) * (p - 1) + Units(r[4][i]!.Value),
                    Grid * p
                );
            if (i >= lag && r[5][i].HasValue && r[5][i - lag].HasValue)
                r[6][i] = Round(Units(r[5][i]!.Value) + Units(r[5][i - lag]!.Value), 2 * Grid);
        }
        return r;
    }

    internal static decimal?[][] DecimalReference(CompetitorData data, int p, int lag)
    {
        var bars = data.Candles.ToArray();
        var n = bars.Length;
        var r = Enumerable.Range(0, 7).Select(_ => new decimal?[n]).ToArray();
        var inputs = new[] { new decimal[n], new decimal[n], new decimal[n] };
        var mean = new decimal[3];
        var alpha = 1.0m / p;
        for (var i = 0; i < n; i++)
        {
            r[4][i] = 0m;
            if (i == 0)
                continue;
            var up = bars[i].High - bars[i - 1].High;
            var down = bars[i - 1].Low - bars[i].Low;
            r[0][i] = up;
            r[1][i] = down;
            inputs[0][i] = up > 0 && up > down ? up : 0;
            inputs[1][i] = down > 0 && down > up ? down : 0;
            inputs[2][i] = Math.Max(
                bars[i].High - bars[i].Low,
                Math.Max(
                    Math.Abs(bars[i].High - bars[i - 1].Close),
                    Math.Abs(bars[i].Low - bars[i - 1].Close)
                )
            );
            if (i < p)
                continue;
            for (var j = 0; j < 3; j++)
                mean[j] =
                    i == p
                        ? inputs[j].Skip(1).Take(p).Average()
                        : mean[j] + alpha * (inputs[j][i] - mean[j]);
            if (mean[2] != 0)
            {
                var plus = mean[0] / mean[2] * 100;
                var minus = mean[1] / mean[2] * 100;
                r[2][i] = plus;
                r[3][i] = minus;
                r[4][i] =
                    plus + minus == 0 ? null : Math.Abs((plus - minus) / (plus + minus)) * 100;
            }
            r[5][i] =
                i == p
                    ? r[4].Skip(1).Take(p).Average()
                    : r[5][i - 1] + alpha * (r[4][i] - r[5][i - 1]);
            if (i >= lag)
                r[6][i] = (r[5][i] + r[5][i - lag]) / 2;
        }
        return r;
    }
}
