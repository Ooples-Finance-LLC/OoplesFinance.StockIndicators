using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DeviationRatioComparison
{
    internal static ComparisonPair Pair(int longPeriod = 0, double alpha = .2) =>
        new(
            "QuanTAlib.Vidya",
            nameof(DeviationRatioAdaptiveAverage),
            (d, p) => Series(Native(d.Closes, p, longPeriod, alpha)),
            (d, p) => Owned(d.IndicatorBars, p, longPeriod, alpha),
            (d, p) => Series(GridReference(d.Closes, p, longPeriod, alpha)),
            CompetitorReference: (d, p) => Series(NativeReference(d.Closes, p, longPeriod, alpha)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    // The documented native NaN recurrence is represented as absent in the pair;
    // direct tests retain and compare the raw NaNs. Infinities are never hidden.
    internal static ComparisonSeries Series(double[] values) =>
        new(
            new Dictionary<string, ComparisonOutput>
            {
                ["Value"] = new(0, values, values.Select(v => !double.IsNaN(v)).ToArray()),
            }
        );

    internal static ComparisonSeries Owned(Bar[] bars, int p, int length, double alpha)
    {
        var indicator = new DeviationRatioAdaptiveAverage(p, length, alpha);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Average].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        for (var i = 0; i < values.Length; i++)
            if (present[i] == 0)
                values[i] = double.NaN;
        return Series(values);
    }

    internal static double[] Native(double[] values, int p, int length, double alpha)
    {
        var indicator = new QuanTAlib.Vidya(p, length, alpha);
        return values
            .Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
            .ToArray();
    }

    internal static double[] NativeReference(double[] values, int p, int length, double alpha)
    {
        length = length == 0 ? checked(p * 4) : length;
        var result = new double[values.Length];
        var previous = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            var shortWindow = values
                .Skip(Math.Max(0, i - p + 1))
                .Take(Math.Min(p, i + 1))
                .ToArray();
            var shortMean = Mean(shortWindow);
            if (i < length)
                previous = shortMean;
            else
            {
                var longWindow = values
                    .Skip(Math.Max(0, i - length + 1))
                    .Take(Math.Min(length, i + 1))
                    .ToArray();
                var longMean = Mean(longWindow);
                var shortVariance =
                    shortWindow.Sum(v => Math.Pow(v - shortMean, 2)) / shortWindow.Length;
                var longVariance =
                    longWindow.Sum(v => Math.Pow(v - longMean, 2)) / longWindow.Length;
                var gain = alpha * (Math.Sqrt(shortVariance) / Math.Sqrt(longVariance));
                previous = gain * values[i] + (1 - gain) * previous;
            }
            result[i] = previous;
        }
        return result;
    }

    private static double Mean(double[] values)
    {
        var width = Vector<double>.Count;
        var lanes = new double[width];
        var offset = 0;
        for (; offset <= values.Length - width; offset += width)
        for (var lane = 0; lane < width; lane++)
            lanes[lane] += values[offset + lane];
        var sum = 0d;
        foreach (var lane in lanes)
            sum += lane;
        for (; offset < values.Length; offset++)
            sum += values[offset];
        return sum / values.Length;
    }

    internal static double[] GridReference(double[] values, int p, int length, double alpha)
    {
        var result = Enumerable.Repeat(double.NaN, values.Length).ToArray();
        length = length == 0 ? checked(4 * p) : length;
        var previous = BigInteger.Zero;
        for (var i = 0; i < values.Length; i++)
        {
            var shortWindow = values
                .Skip(Math.Max(0, i - p + 1))
                .Take(Math.Min(p, i + 1))
                .Select(Units)
                .ToArray();
            if (i < length)
                previous = DirectionalComparison.RoundedUnits(
                    shortWindow.Aggregate(BigInteger.Zero, (a, b) => a + b),
                    shortWindow.Length
                );
            else
            {
                var longWindow = values
                    .Skip(Math.Max(0, i - length + 1))
                    .Take(Math.Min(length, i + 1))
                    .Select(Units)
                    .ToArray();
                var longSpread = Spread(longWindow);
                if (longSpread.IsZero)
                    break;
                var numerator = Spread(shortWindow) * longWindow.Length * longWindow.Length;
                var denominator = longSpread * shortWindow.Length * shortWindow.Length;
                var shift = 0;
                var ratio = DispersionReferenceArithmetic.Sqrt(numerator, denominator);
                while (double.IsInfinity(ratio))
                {
                    denominator <<= 512;
                    shift += 256;
                    ratio = DispersionReferenceArithmetic.Sqrt(numerator, denominator);
                }
                var gain = DirectionalComparison.RoundedUnits(
                    Units(alpha) * (Units(ratio) << shift),
                    Grid
                );
                previous = DirectionalComparison.RoundedUnits(
                    gain * Units(values[i]) + (Grid - gain) * previous,
                    Grid
                );
            }
            result[i] = Round(previous, Grid);
        }
        return result;
    }

    // Recompute centered deviations for each window, independently of rolling raw moments.
    private static BigInteger Spread(BigInteger[] values)
    {
        var total = values.Aggregate(BigInteger.Zero, (a, b) => a + b);
        return values.Aggregate(
                BigInteger.Zero,
                (a, b) =>
                {
                    var deviation = values.Length * b - total;
                    return a + deviation * deviation;
                }
            ) / values.Length;
    }
}
