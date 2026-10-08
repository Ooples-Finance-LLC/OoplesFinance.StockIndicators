using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SampleShapeComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static ComparisonPair Create(bool kurtosis) =>
        new(
            kurtosis ? "QuanTAlib.Kurtosis" : "QuanTAlib.Skew",
            nameof(WindowSampleShape),
            (d, p) => Native(d, p, kurtosis),
            (d, p) => Owned(d.IndicatorBars, p, kurtosis),
            (d, p) => VolumePriceComparison.Mask(Reference(d.Closes, p, kurtosis)),
            CompetitorReference: (d, p) => new(0, NativeReference(d.Closes, p, kurtosis)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static QuanTAlib.AbstractBase NativeIndicator(int period, bool kurtosis) =>
        kurtosis ? new QuanTAlib.Kurtosis(period) : new QuanTAlib.Skew(period);

    private static ComparisonSeries Native(CompetitorData data, int period, bool kurtosis)
    {
        var indicator = NativeIndicator(period, kurtosis);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool kurtosis)
    {
        var indicator = new WindowSampleShape(period, kurtosis);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(double[] prices, int period, bool kurtosis)
    {
        var result = new double?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var count = Math.Min(i + 1, period);
            if (count < (kurtosis ? 4 : 3))
            {
                result[i] = 0;
                continue;
            }
            var values = prices.Skip(i - count + 1).Take(count).Select(Units).ToArray();
            var n = new BigInteger(count);
            var total = values.Aggregate(BigInteger.Zero, (s, v) => s + v);
            var deviations = values.Select(x => n * x - total).ToArray();
            var squares = deviations.Aggregate(BigInteger.Zero, (s, d) => s + d * d);
            if (squares.IsZero)
            {
                result[i] = kurtosis ? null : 0;
                continue;
            }
            var moment = deviations.Aggregate(
                BigInteger.Zero,
                (s, d) => s + BigInteger.Pow(d, kurtosis ? 4 : 3)
            );
            result[i] = kurtosis
                ? Round(
                    n * (n + 1) * (n - 1) * moment - 3 * (n - 1) * (n - 1) * squares * squares,
                    squares * squares * (n - 2) * (n - 3)
                )
                : moment.Sign
                    * DispersionReferenceArithmetic.Sqrt(
                        n * n * (n - 1) * moment * moment,
                        (n - 2) * (n - 2) * squares * squares * squares
                    );
        }
        return result;
    }

    internal static double[] NativeReference(double[] prices, int period, bool kurtosis)
    {
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var count = Math.Min(i + 1, period);
            if (count < (kurtosis ? 4 : 3))
                continue;
            var values = prices.Skip(i - count + 1).Take(count).ToArray();
            var mean = Divide(values.Aggregate(0d, Add), count);
            var s2 = 0d;
            var high = 0d;
            foreach (var value in values)
            {
                var d = Subtract(value, mean);
                s2 = Add(s2, kurtosis ? Multiply(d, d) : Math.Pow(d, 2));
                high = Add(
                    high,
                    kurtosis ? Multiply(Multiply(Multiply(d, d), d), d) : Math.Pow(d, 3)
                );
            }
            if (kurtosis)
            {
                if (s2 == 0)
                {
                    result[i] = double.NaN;
                    continue;
                }
                var variance = Divide(s2, count - 1);
                var numerator = Multiply(Multiply(count, count + 1), high);
                var denominator = Multiply(
                    Multiply(Multiply(Multiply(variance, variance), count - 3), count - 1),
                    count - 2
                );
                result[i] = Subtract(
                    Divide(numerator, denominator),
                    Divide(
                        Multiply(Multiply(3, count - 1), count - 1),
                        Multiply(count - 2, count - 3)
                    )
                );
            }
            else
            {
                var s3 = Math.Pow(Divide(s2, count), 1.5);
                result[i] =
                    s3 == 0
                        ? 0
                        : Multiply(
                            Divide(Math.Sqrt(Multiply(count, count - 1)), count - 2),
                            Divide(Divide(high, count), s3)
                        );
            }
        }
        return result;
    }

    internal static CompetitorData Fixture(int count = 31) =>
        CompetitorData.FromCloses(
            Enumerable
                .Range(0, count)
                .Select(i => new[] { 3d, 12, -6, 9, 18, -3, 4, -18, 20 }[i % 9] + i / 16d)
                .ToArray()
        );
}
