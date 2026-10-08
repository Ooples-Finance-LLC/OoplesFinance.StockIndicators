using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SumDirectionalComparison
{
    internal static readonly string[] Names = ["Pdi", "Mdi", "Dx", "Adx", "Adxr"];
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetAdx",
        nameof(SumSeededDirectionalIndex),
        (d, p) => Native(d.Quotes.GetAdx(p)),
        (d, p) => Owned(d.IndicatorBars, p),
        (d, p) => Series(Reference(d.IndicatorBars, p)),
        Names,
        CompetitorReference: (d, p) =>
            Series(
                NativeReference(
                    d.Quotes.Select(q => new Bar(
                            q.Date,
                            (double)q.Open,
                            (double)q.High,
                            (double)q.Low,
                            (double)q.Close,
                            (double)q.Volume
                        ))
                        .ToArray(),
                    p
                )
            ),
        ErrorBudget: IndicatorErrorBudget.Exact
    );

    internal static ComparisonSeries Series(double?[][] values) =>
        RetrospectivePriceComparison.Series(Names, values);

    internal static ComparisonSeries Native(IEnumerable<AdxResult> rows)
    {
        var r = rows.ToArray();
        return Series([
            r.Select(v => v.Pdi).ToArray(),
            r.Select(v => v.Mdi).ToArray(),
            r.Select(v => v.Dx).ToArray(),
            r.Select(v => v.Adx).ToArray(),
            r.Select(v => v.Adxr).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int p)
    {
        var indicator = new SumSeededDirectionalIndex(p);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var result = new double?[5][];
        for (var j = 0; j < 5; j++)
        {
            var values = run[indicator.Outputs[j]].ToArray();
            var flags = run[indicator.Outputs[j + 5]].ToArray();
            result[j] = values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
        }
        return Series(result);
    }

    internal static double?[][] Reference(Bar[] bars, int p)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double?[bars.Length]).ToArray();
        var raw = Enumerable.Range(0, 3).Select(_ => new BigInteger[bars.Length]).ToArray();
        var smoothed = Enumerable.Range(0, 3).Select(_ => new BigInteger[bars.Length]).ToArray();
        for (var i = 1; i < bars.Length; i++)
        {
            var up = Units(bars[i].High) - Units(bars[i - 1].High);
            var down = Units(bars[i - 1].Low) - Units(bars[i].Low);
            raw[0][i] = DirectionalComparison.RoundedUnits(up > 0 && up > down ? up : 0, 1);
            raw[1][i] = DirectionalComparison.RoundedUnits(down > 0 && down > up ? down : 0, 1);
            raw[2][i] = DirectionalComparison.RoundedUnits(
                new[]
                {
                    Units(bars[i].High) - Units(bars[i].Low),
                    BigInteger.Abs(Units(bars[i].High) - Units(bars[i - 1].Close)),
                    BigInteger.Abs(Units(bars[i].Low) - Units(bars[i - 1].Close)),
                }.Max(),
                1
            );
        }
        for (var j = 0; j < 3; j++)
        for (var i = p; i < bars.Length; i++)
            smoothed[j][i] =
                i == p
                    ? DirectionalComparison.RoundedUnits(
                        raw[j].Skip(1).Take(p).Aggregate(BigInteger.Zero, (a, b) => a + b),
                        1
                    )
                    : DirectionalComparison.RoundedUnits(
                        smoothed[j][i - 1] * (p - 1) + raw[j][i] * p,
                        p
                    );
        double? previous = null;
        for (var i = p; i < bars.Length; i++)
        {
            if (smoothed[2][i].IsZero)
                continue;
            var plus = DirectionalComparison.RoundedUnits(
                100 * smoothed[0][i] * Grid,
                smoothed[2][i]
            );
            var minus = DirectionalComparison.RoundedUnits(
                100 * smoothed[1][i] * Grid,
                smoothed[2][i]
            );
            result[0][i] = Round(plus, Grid);
            result[1][i] = Round(minus, Grid);
            result[2][i] = (plus + minus).IsZero
                ? 0
                : Round(100 * BigInteger.Abs(plus - minus), plus + minus);
            if (i == 2L * p - 1)
                previous = Round(
                    result[2]
                        .Skip(p)
                        .Take(p)
                        .Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v ?? 0)),
                    Grid * p
                );
            else if (i >= 2L * p && previous.HasValue)
                previous = Round(
                    Units(previous.Value) * (p - 1) + Units(result[2][i]!.Value),
                    Grid * p
                );
            result[3][i] = previous;
            if (previous.HasValue && result[3][i - p].HasValue)
                result[4][i] = Round(
                    Units(previous.Value) + Units(result[3][i - p]!.Value),
                    2 * Grid
                );
        }
        return result;
    }

    // Native oracle retains chronological binary64 sums and separate arithmetic
    // stages. Its source is an independently constructed table of range/movement
    // values, rather than the streaming production state.
    internal static double?[][] NativeReference(Bar[] bars, int p)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double?[bars.Length]).ToArray();
        var raw = Enumerable.Range(0, 3).Select(_ => new double[bars.Length]).ToArray();
        var smooth = Enumerable.Range(0, 3).Select(_ => new double[bars.Length]).ToArray();
        for (var i = 1; i < bars.Length; i++)
        {
            var up = Subtract(bars[i].High, bars[i - 1].High);
            var down = Subtract(bars[i - 1].Low, bars[i].Low);
            raw[0][i] = up > down ? Math.Max(up, 0) : 0;
            raw[1][i] = down > up ? Math.Max(down, 0) : 0;
            raw[2][i] = Math.Max(
                Subtract(bars[i].High, bars[i].Low),
                Math.Max(
                    Math.Abs(Subtract(bars[i].High, bars[i - 1].Close)),
                    Math.Abs(Subtract(bars[i].Low, bars[i - 1].Close))
                )
            );
        }
        for (var j = 0; j < 3; j++)
        for (var i = p; i < bars.Length; i++)
            smooth[j][i] =
                i == p
                    ? raw[j].Skip(1).Take(p).Aggregate(0d, Add)
                    : Add(Subtract(smooth[j][i - 1], Divide(smooth[j][i - 1], p)), raw[j][i]);
        double? previous = null;
        double seed = 0;
        for (var i = p; i < bars.Length; i++)
        {
            if (smooth[2][i] == 0) // NOSONAR: Native zero-range branch is exact.
                continue;
            var plus = Divide(Multiply(100, smooth[0][i]), smooth[2][i]);
            var minus = Divide(Multiply(100, smooth[1][i]), smooth[2][i]);
            var total = Add(plus, minus);
            var dx = total == 0 ? 0 : Divide(Multiply(100, Math.Abs(Subtract(plus, minus))), total); // NOSONAR: Native zero directional sum.
            result[0][i] = plus;
            result[1][i] = minus;
            result[2][i] = dx;
            if (i < 2L * p)
            {
                seed = Add(seed, dx);
                if (i == 2L * p - 1)
                    previous = Divide(seed, p);
            }
            else if (previous.HasValue)
                previous = Divide(Add(Multiply(previous.Value, p - 1), dx), p);
            result[3][i] = previous;
            if (previous.HasValue && result[3][i - p].HasValue)
                result[4][i] = Divide(Add(previous.Value, result[3][i - p]!.Value), 2);
        }
        return result;
    }
}
