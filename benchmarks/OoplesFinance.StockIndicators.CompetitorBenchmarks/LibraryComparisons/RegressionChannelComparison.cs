using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RegressionChannelComparison
{
    internal static readonly string[] Names =
    [
        "Centerline",
        "UpperChannel",
        "LowerChannel",
        "BreakPoint",
    ];

    internal static ComparisonPair Pair(bool whole = false, double deviations = 2) =>
        new(
            "Skender.GetStdDevChannels",
            nameof(RegressionChannelSnapshot),
            (d, p) => Native(d.Closes, whole ? null : p, deviations),
            (d, p) => Owned(d.IndicatorBars, whole ? null : p, deviations),
            (d, p) => Series(Reference(d.Closes, whole ? null : p, deviations, false)),
            Names,
            MinimumInputCount: whole ? 2 : 0,
            CompetitorReference: (d, p) =>
                Series(Reference(d.Closes, whole ? null : p, deviations, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(double[] prices, int? period, double deviations)
    {
        var rows = prices
            .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
            .GetStdDevChannels(period, deviations)
            .ToArray();
        return Series([
            rows.Select(r => r.Centerline).ToArray(),
            rows.Select(r => r.UpperChannel).ToArray(),
            rows.Select(r => r.LowerChannel).ToArray(),
            rows.Select(r => (double?)(r.BreakPoint ? 1 : 0)).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int? period, double deviations)
    {
        var rows = RegressionChannelSnapshot.Calculate(bars, period, deviations);
        return Series([
            rows.Select(r => r.Centerline).ToArray(),
            rows.Select(r => r.UpperChannel).ToArray(),
            rows.Select(r => r.LowerChannel).ToArray(),
            rows.Select(r => (double?)(r.BreakPoint ? 1 : 0)).ToArray(),
        ]);
    }

    internal static double?[][] Reference(
        double[] prices,
        int? period,
        double deviations,
        bool native
    )
    {
        var length = period ?? prices.Length;
        if (length < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        var result = Enumerable.Range(0, 4).Select(_ => new double?[prices.Length]).ToArray();
        Array.Fill(result[3], 0d);
        for (var end = prices.Length - 1; end >= length - 1; end -= length)
        {
            var start = end - length + 1;
            if (native)
            {
                double sx = 0,
                    sy = 0;
                for (var i = start; i <= end; i++)
                {
                    sx += i + 1d;
                    sy += prices[i];
                }
                var mx = sx / length;
                var my = sy / length;
                double xx = 0,
                    yy = 0,
                    xy = 0;
                for (var i = start; i <= end; i++)
                {
                    var dx = i + 1d - mx;
                    var dy = prices[i] - my;
                    xx += dx * dx;
                    yy += dy * dy;
                    xy += dx * dy;
                }
                double? slope = xy / xx;
                if (double.IsNaN(slope.Value))
                    slope = null;
                double? intercept = my - slope * mx;
                if (intercept.HasValue && double.IsNaN(intercept.Value))
                    intercept = null;
                double? deviation = Math.Sqrt(yy / length);
                if (double.IsNaN(deviation.Value))
                    deviation = null;
                for (var i = start; i <= end; i++)
                {
                    var center = slope * (i + 1) + intercept;
                    // CalcSlope builds a decimal final-window overlay even though channels do not use it.
                    if (end == prices.Length - 1 && center.HasValue && !double.IsNaN(center.Value))
                        _ = (decimal)center.Value;
                    result[0][i] = center;
                    result[1][i] = center + deviations * deviation;
                    result[2][i] = center - deviations * deviation;
                }
            }
            else
            {
                var w = prices.Skip(start).Take(length).Select(Units).ToArray();
                var n = new BigInteger(length);
                var sum = w.Aggregate(BigInteger.Zero, (s, v) => s + v);
                var square = w.Aggregate(BigInteger.Zero, (s, v) => s + v * v);
                var covariance = w.Select((v, i) => v * (2 * i - (n - 1)))
                    .Aggregate(BigInteger.Zero, (s, v) => s + v);
                var denominator = n * (n * n - 1);
                var deviation = DispersionReferenceArithmetic.Sqrt(
                    n * square - sum * sum,
                    n * n * Grid * Grid
                );
                var width = DirectionalComparison.RoundedUnits(
                    Units(deviation) * Units(deviations),
                    Grid
                );
                for (var j = 0; j < length; j++)
                {
                    var center = Round(
                        sum * (n * n - 1) + 3 * covariance * (2 * j - (n - 1)),
                        denominator * Grid
                    );
                    result[0][start + j] = center;
                    result[1][start + j] = Round(Units(center) + width, Grid);
                    result[2][start + j] = Round(Units(center) - width, Grid);
                }
            }
            result[3][start] = 1;
        }
        return result;
    }
}
