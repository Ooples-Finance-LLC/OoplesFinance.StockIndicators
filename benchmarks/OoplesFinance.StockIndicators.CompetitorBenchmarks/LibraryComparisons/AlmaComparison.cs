using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AlmaComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(true), Create(false)];

    internal static ComparisonPair Create(bool skender, double offset = .85, double sigma = 6) =>
        new(
            skender ? "Skender.GetAlma" : "QuanTAlib.Alma",
            nameof(ArnaudLegouxWindow),
            (d, p) => Native(d, p, skender, offset, sigma),
            (d, p) => Owned(d.IndicatorBars, p, skender, offset, sigma),
            (d, p) => Reference(d.Closes, p, skender, offset, sigma, false),
            CompetitorReference: (d, p) =>
                Reference(
                    skender ? d.Quotes.Select(q => (double)q.Close).ToArray() : d.Closes,
                    p,
                    skender,
                    offset,
                    sigma,
                    true
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool skender,
        double offset,
        double sigma
    )
    {
        if (skender)
            return VolumePriceComparison.Mask(
                data.Quotes.GetAlma(period, offset, sigma).Select(r => r.Alma).ToArray()
            );
        var indicator = new QuanTAlib.Alma(period, offset, sigma);
        return VolumePriceComparison.Mask(
            data.Closes.Select(x =>
                    (double?)indicator.Calc(new QuanTAlib.TValue(x, true, false)).Value
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int period,
        bool fullWindow,
        double offset = .85,
        double sigma = 6
    )
    {
        var indicator = new ArnaudLegouxWindow(period, offset, sigma, fullWindow);
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

    internal static ComparisonSeries Reference(
        double[] prices,
        int period,
        bool fullWindow,
        double offset,
        double sigma,
        bool native
    )
    {
        var result = new double?[prices.Length];
        var kernels = new Dictionary<int, double[]>();
        for (var end = 0; end < prices.Length; end++)
        {
            var count = Math.Min(period, end + 1);
            if (fullWindow && count < period)
                continue;
            if (!kernels.TryGetValue(count, out var weights))
                kernels[count] = weights = Weights(count, offset, sigma, native);
            if (native)
            {
                double sum = 0,
                    mass = 0;
                for (var i = 0; i < count; i++)
                {
                    sum = Plus(sum, Times(weights[i], prices[end - count + 1 + i]));
                    mass = Plus(mass, weights[i]);
                }
                var value = Quotient(sum, mass);
                result[end] = fullWindow && double.IsNaN(value) ? null : value;
            }
            else
            {
                BigInteger sum = 0,
                    mass = 0;
                for (var i = 0; i < count; i++)
                {
                    var weight = Units(weights[i]);
                    sum += weight * Units(prices[end - count + 1 + i]);
                    mass += weight;
                }
                result[end] = Round(sum, mass * Grid);
            }
        }
        return VolumePriceComparison.Mask(result);
    }

    internal static double[] Weights(int count, double offset, double sigma, bool native)
    {
        if (native)
        {
            var center = Times(offset, count - 1);
            var s = Quotient(count, sigma);
            return Enumerable
                .Range(0, count)
                .Select(i =>
                {
                    var delta = Minus(i, center);
                    return Math.Exp(Quotient(-Times(delta, delta), Times(Times(2, s), s)));
                })
                .ToArray();
        }
        var exactCenter = Units(offset) * (count - 1);
        // Direct squared distances and an exhaustive minimum are independent of the
        // runtime's clamped nearest-index selection and factored difference of squares.
        var distances = Enumerable
            .Range(0, count)
            .Select(i => BigInteger.Pow(i * Grid - exactCenter, 2))
            .ToArray();
        var minimum = distances.Min();
        var inverseWidth = BigInteger.Pow(Units(sigma), 2);
        var divisor = 2 * new BigInteger(count) * count * BigInteger.Pow(Grid, 4);
        return distances
            .Select(distance => Math.Exp(-Round((distance - minimum) * inverseWidth, divisor)))
            .ToArray();
    }

    private static double Plus(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Add(a, b) : a + b;

    private static double Minus(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Subtract(a, b) : a - b;

    private static double Times(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Multiply(a, b) : a * b;

    private static double Quotient(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) && b != 0 ? Divide(a, b) : a / b; // NOSONAR: Exact zero selects IEEE undefined/infinite behavior.

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 2, 4, 8, 3, -2, 1, 1, 1, 1, 0, 3, 9, -1]);
}
