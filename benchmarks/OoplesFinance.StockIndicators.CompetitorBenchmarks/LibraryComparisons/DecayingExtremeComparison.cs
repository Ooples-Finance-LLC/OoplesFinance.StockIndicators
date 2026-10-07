using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DecayingExtremeComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static ComparisonPair Create(bool maximum, double decay = 0) =>
        new(
            maximum ? "QuanTAlib.Max" : "QuanTAlib.Min",
            nameof(DecayingWindowExtreme),
            (d, p) => Native(d, p, maximum, decay),
            (d, p) => Owned(d.IndicatorBars, p, maximum, decay),
            (d, p) => new(0, Reference(d.Closes, p, maximum, decay, false)),
            CompetitorReference: (d, p) => new(0, Reference(d.Closes, p, maximum, decay, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static QuanTAlib.AbstractBase NativeIndicator(
        int period,
        bool maximum,
        double decay
    ) => maximum ? new QuanTAlib.Max(period, decay) : new QuanTAlib.Min(period, decay);

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool maximum,
        double decay
    )
    {
        var indicator = NativeIndicator(period, maximum, decay);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool maximum, double decay)
    {
        var indicator = new DecayingWindowExtreme(period, maximum, decay);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(
        double[] prices,
        int period,
        bool maximum,
        double decay,
        bool native
    )
    {
        var result = new double[prices.Length];
        var state = maximum ? double.MinValue : double.MaxValue;
        var last = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            var window = prices
                .Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(i + 1, period))
                .ToArray();
            var bound = maximum ? window.Max() : window.Min();
            if (i == 0 || (maximum ? prices[i] >= state : prices[i] <= state))
            {
                state = prices[i];
                last = i;
            }
            var rate = 1 - Math.Exp(-(decay * .1) * (i - last) / period);
            double mean,
                next;
            if (native)
            {
                // CircularBuffer sums chronological values in SIMD lanes, then its scalar tail.
                var width = Vector<double>.Count;
                var lanes = new double[width];
                var at = 0;
                for (; at <= window.Length - width; at += width)
                for (var lane = 0; lane < width; lane++)
                    lanes[lane] = Add(lanes[lane], window[at + lane]);
                var sum = lanes.Aggregate(0d, Add);
                for (; at < window.Length; at++)
                    sum = Add(sum, window[at]);
                mean = Divide(sum, window.Length);
                next = maximum
                    ? Subtract(state, Multiply(rate, Subtract(state, mean)))
                    : Add(state, Multiply(rate, Subtract(mean, state)));
            }
            else
            {
                mean = Round(
                    window.Aggregate(BigInteger.Zero, (s, v) => s + Units(v)),
                    Grid * window.Length
                );
                next = Round(
                    Units(state) * (Grid - Units(rate)) + Units(mean) * Units(rate),
                    Grid * Grid
                );
            }
            state = maximum ? Math.Min(next, bound) : Math.Max(next, bound);
            result[i] = state;
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([10, 10, 2, 1, 3, 4, 4, 3, -8, 5, 0, 5, 5, -3, 2]);
}
