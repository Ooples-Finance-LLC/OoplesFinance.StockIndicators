using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RangeAdaptiveComparison
{
    internal static ComparisonPair Pair(bool fractal, double scale = .9) =>
        new(
            fractal ? "QuanTAlib.Frama" : "QuanTAlib.Dsma",
            fractal ? nameof(RangeFractalAverage) : nameof(FilteredDeviationAverage),
            (d, p) => Native(d.Closes, p, fractal, scale),
            (d, p) => Owned(d.IndicatorBars, p, fractal, scale),
            (d, p) => new(0, GridReference(d.Closes, p, fractal, scale)),
            CompetitorReference: (d, p) => new(0, NativeReference(d.Closes, p, fractal, scale)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static IndicatorBase Indicator(int period, bool fractal, double scale = .9) =>
        fractal ? new RangeFractalAverage(period) : new FilteredDeviationAverage(period, scale);

    internal static QuanTAlib.AbstractBase NativeIndicator(
        int period,
        bool fractal,
        double scale = .9
    ) => fractal ? new QuanTAlib.Frama(period) : new QuanTAlib.Dsma(period, scale);

    internal static ComparisonSeries Native(double[] prices, int period, bool fractal, double scale)
    {
        var owner = NativeIndicator(period, fractal, scale);
        return new(
            0,
            prices.Select(v => owner.Calc(new QuanTAlib.TValue(v, true, false)).Value).ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool fractal, double scale = .9)
    {
        var owner = Indicator(period, fractal, scale);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[owner.Outputs[0]].ToArray());
    }

    private static double NativeSum(double[] values)
    {
        var lanes = new double[Vector<double>.Count];
        var i = 0;
        for (; i <= values.Length - lanes.Length; i += lanes.Length)
        for (var j = 0; j < lanes.Length; j++)
            lanes[j] += values[i + j];
        double sum = 0;
        foreach (var lane in lanes)
            sum += lane;
        for (; i < values.Length; i++)
            sum += values[i];
        return sum;
    }

    internal static double[] NativeReference(
        double[] prices,
        int period,
        bool fractal,
        double scale
    )
    {
        var result = new double[prices.Length];
        var filter = new double[prices.Length];
        var zero = new double[prices.Length];
        double prior = 0;
        var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
        var c2 = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;
        for (var i = 0; i < prices.Length; i++)
        {
            if (fractal)
            {
                var w = prices
                    .Skip(Math.Max(0, i - period + 1))
                    .Take(Math.Min(i + 1, period))
                    .ToArray();
                if (w.Length < period)
                    prior = NativeSum(w) / w.Length;
                else
                {
                    var half = period / 2;
                    var first = w.Take(half).ToArray();
                    var last = w.Skip(half).ToArray();
                    var n1 = (w.Max() - w.Min()) / period;
                    var n2 = (first.Max() - first.Min() + last.Max() - last.Min()) / half;
                    var d =
                        (Math.Log(n2 + double.Epsilon) - Math.Log(n1 + double.Epsilon))
                        / Math.Log(2);
                    var gain = Math.Max(Math.Min(Math.Exp(-4.6 * (d - 1)), 1), .01);
                    prior = gain * (prices[i] - prior) + prior;
                }
            }
            else if (i == 0)
                prior = prices[i];
            else
            {
                zero[i] = prices[i] - prior;
                filter[i] =
                    c1 * (zero[i] + zero[i - 1]) / 2
                    + c2 * filter[i - 1]
                    + c3 * (i > 1 ? filter[i - 2] : 0);
                var rms = Math.Sqrt(
                    NativeSum(
                        filter
                            .Skip(Math.Max(1, i - period + 1))
                            .Take(Math.Min(i, period))
                            .Select(f => f * f)
                            .ToArray()
                    ) / period
                );
                var normalized = rms != 0 ? filter[i] / rms : 0;
                var gain = Math.Max(.1, Math.Min(1, scale * Math.Abs(normalized) * 5 / period));
                prior = gain * prices[i] + (1 - gain) * prior;
            }
            result[i] = prior;
        }
        return result;
    }

    internal static double[] GridReference(
        double[] prices,
        int period,
        bool fractal,
        double scale = .9
    )
    {
        var result = new double[prices.Length];
        var p = prices.Select(Units).ToArray();
        var filters = new BigInteger[p.Length];
        var zeros = new BigInteger[p.Length];
        BigInteger prior = 0;
        var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
        var b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
        var c = -a * a;
        var c1 = Units(1 - b - c);
        var c2 = Units(b);
        var c3 = Units(c);
        BigInteger R(BigInteger n, BigInteger d) => DirectionalComparison.RoundedUnits(n, d);
        for (var i = 0; i < p.Length; i++)
        {
            if (fractal)
            {
                var w = p.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(i + 1, period)).ToArray();
                if (w.Length < period)
                    prior = R(w.Aggregate(BigInteger.Zero, (s, v) => s + v), w.Length);
                else
                {
                    var half = period / 2;
                    var left = w.Take(half).ToArray();
                    var right = w.Skip(half).ToArray();
                    var ratio = Round(
                        (left.Max() - left.Min() + right.Max() - right.Min() + half) * period,
                        (w.Max() - w.Min() + period) * half
                    );
                    var gain = Units(
                        Math.Clamp(Math.Exp(-4.6 * (Math.Log(ratio) / Math.Log(2) - 1)), .01, 1)
                    );
                    prior = R(gain * p[i] + (Grid - gain) * prior, Grid);
                }
            }
            else if (i == 0)
                prior = p[i];
            else
            {
                zeros[i] = R(p[i] - prior, 1);
                filters[i] = R(
                    c1 * (zeros[i] + zeros[i - 1])
                        + 2 * c2 * filters[i - 1]
                        + 2 * c3 * (i > 1 ? filters[i - 2] : 0),
                    2 * Grid
                );
                var sum = filters
                    .Skip(Math.Max(1, i - period + 1))
                    .Take(Math.Min(i, period))
                    .Aggregate(BigInteger.Zero, (s, f) => s + f * f);
                var normalized = sum.IsZero
                    ? 0
                    : DispersionReferenceArithmetic.Sqrt(filters[i] * filters[i] * period, sum);
                var gain = Units(Math.Clamp(scale * normalized * 5 / period, .1, 1));
                prior = R(gain * p[i] + (Grid - gain) * prior, Grid);
            }
            result[i] = Round(prior, Grid);
        }
        return result;
    }
}
