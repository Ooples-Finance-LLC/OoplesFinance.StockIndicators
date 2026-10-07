using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DeviationBandsComparison
{
    internal static readonly string[] Ids =
    [
        "Skender.GetBollingerBands",
        "Trady.Indicator.BollingerBands",
        "Trady.Indicator.BollingerBandWidth",
    ];
    internal static readonly ComparisonPair[] Pairs = Ids.Select(id => Pair(id)).ToArray();

    internal static string[] Names(string id) =>
        id == Ids[0] ? ["Sma", "UpperBand", "LowerBand", "PercentB", "ZScore", "Width"]
        : id == Ids[1] ? ["MiddleBand", "UpperBand", "LowerBand"]
        : ["Value"];

    internal static ComparisonPair Pair(string id, double factor = 2) =>
        new(
            id,
            nameof(WindowDeviationBands),
            (d, p) => Native(id, d, p, factor),
            (d, p) => Owned(id, d.IndicatorBars, p, factor),
            (d, p) => Series(id, GridReference(d.Closes, p, factor, id != Ids[0])),
            Names(id),
            CompetitorReference: (d, p) =>
                Series(id, NativeReference(d.Closes, p, factor, id != Ids[0])),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(string id, double?[][] values)
    {
        var slots =
            id == Ids[0] ? new[] { 0, 1, 2, 3, 4, 5 }
            : id == Ids[1] ? new[] { 0, 1, 2 }
            : new[] { 5 };
        return new(
            Names(id)
                .Select(
                    (name, i) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slots[i]].Select(v => v ?? double.NaN).ToArray(),
                                values[slots[i]].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(v => v.Key, v => v.Value)
        );
    }

    internal static ComparisonSeries Owned(string id, Bar[] bars, int p, double factor)
    {
        var indicator = new WindowDeviationBands(p, factor, id != Ids[0]);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            id,
            Enumerable
                .Range(0, 6)
                .Select(slot =>
                {
                    var v = run[indicator.Outputs[slot]].ToArray();
                    var mask = run[indicator.Outputs[slot + 6]].ToArray();
                    return v.Select((x, i) => mask[i] > 0 ? (double?)x : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(string id, CompetitorData d, int p, double factor)
    {
        if (id == Ids[0])
            return SkenderSeries(d.Quotes.GetBollingerBands(p, factor).ToArray());
        if (id == Ids[1])
        {
            var indicator = new Trady.Analysis.Indicator.BollingerBands(
                d.Candles,
                p,
                (decimal)factor
            );
            return TradySeries(indicator.Compute().Select(v => v.Tick).ToArray());
        }
        var width = new Trady.Analysis.Indicator.BollingerBandWidth(d.Candles, p, (decimal)factor);
        return WidthSeries(width.Compute().Select(v => v.Tick).ToArray());
    }

    internal static ComparisonSeries SkenderSeries(
        Skender.Stock.Indicators.BollingerBandsResult[] rows
    ) =>
        Series(
            Ids[0],
            [
                rows.Select(v => v.Sma).ToArray(),
                rows.Select(v => v.UpperBand).ToArray(),
                rows.Select(v => v.LowerBand).ToArray(),
                rows.Select(v => v.PercentB).ToArray(),
                rows.Select(v => v.ZScore).ToArray(),
                rows.Select(v => v.Width).ToArray(),
            ]
        );

    internal static ComparisonSeries TradySeries(
        (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand)[] rows
    ) =>
        Series(
            Ids[1],
            [
                rows.Select(v => (double?)v.MiddleBand).ToArray(),
                rows.Select(v => (double?)v.UpperBand).ToArray(),
                rows.Select(v => (double?)v.LowerBand).ToArray(),
            ]
        );

    internal static ComparisonSeries WidthSeries(decimal?[] rows) =>
        new(
            new Dictionary<string, ComparisonOutput>
            {
                ["Value"] = new(
                    0,
                    rows.Select(v => v.HasValue ? (double)v.Value : double.NaN).ToArray(),
                    rows.Select(v => v.HasValue).ToArray()
                ),
            }
        );

    internal static double?[][] NativeReference(
        double[] input,
        int p,
        double factor,
        bool trady,
        bool quoteConversion = true
    )
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double?[input.Length]).ToArray();
        for (var i = p - 1; i < input.Length; i++)
        {
            if (trady)
            {
                var window = input.Skip(i - p + 1).Take(p).Select(v => (decimal)v).ToArray();
                var mean = window.Sum() / p;
                var variance = window.Sum(v => (v - mean) * (v - mean)) / p;
                var deviation = (decimal)Math.Sqrt((double)variance);
                var upper = mean + (decimal)factor * deviation;
                var lower = mean - (decimal)factor * deviation;
                result[0][i] = (double)mean;
                result[1][i] = (double)upper;
                result[2][i] = (double)lower;
                if (mean != 0)
                    result[5][i] = (double)((upper - lower) / mean * 100);
            }
            else
            {
                var window = input
                    .Skip(i - p + 1)
                    .Take(p)
                    .Select(v => quoteConversion ? (double)(decimal)v : v)
                    .ToArray();
                var mean = window.Aggregate(0d, (a, b) => a + b) / p;
                var variance = window.Aggregate(0d, (a, b) => a + (b - mean) * (b - mean)) / p;
                var deviation = Math.Sqrt(variance);
                double? center = double.IsNaN(mean) ? null : mean;
                double? spread = double.IsNaN(deviation) ? null : deviation;
                var upper = center + factor * spread;
                var lower = center - factor * spread;
                result[0][i] = center;
                result[1][i] = upper;
                result[2][i] = lower;
                // Preserve native nullable equality, including NaN being unequal to itself.
                if (!Nullable.Equals(upper, lower) || upper.HasValue && double.IsNaN(upper.Value))
                    result[3][i] = (window[^1] - lower) / (upper - lower);
                if (spread != 0)
                    result[4][i] = (window[^1] - center) / spread;
                if (center != 0)
                    result[5][i] = (upper - lower) / center;
            }
        }
        return result;
    }

    internal static double?[][] GridReference(double[] input, int p, double factor, bool percent)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double?[input.Length]).ToArray();
        for (var i = p - 1; i < input.Length; i++)
        {
            var values = input.Skip(i - p + 1).Take(p).Select(Units).ToArray();
            var n = new BigInteger(p);
            var sum = values.Aggregate(BigInteger.Zero, (a, b) => a + b);
            var deltas = values.Select(v => n * v - sum).ToArray();
            var squares = deltas.Aggregate(BigInteger.Zero, (a, b) => a + b * b);
            var center = Units(Round(sum, n * Grid));
            var deviation = DispersionReferenceArithmetic.Sqrt(squares, n * n * n * Grid * Grid);
            var half = DirectionalComparison.RoundedUnits(Units(factor) * Units(deviation), Grid);
            result[0][i] = Round(center, Grid);
            result[1][i] = Round(center + half, Grid);
            result[2][i] = Round(center - half, Grid);
            if (!half.IsZero)
                result[3][i] = Round(values[^1] - center + half, 2 * half);
            if (!squares.IsZero)
                result[4][i] =
                    deltas[^1].Sign
                    * DispersionReferenceArithmetic.Sqrt(deltas[^1] * deltas[^1] * n, squares);
            if (!center.IsZero)
                result[5][i] = Round((percent ? 200 : 2) * half, center);
        }
        return result;
    }
}
