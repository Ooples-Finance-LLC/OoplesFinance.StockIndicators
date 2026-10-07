using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class OldestHilbertComparison
{
    internal static ComparisonPair Pair() =>
        new(
            "QuanTAlib.Htit",
            nameof(OldestFirstHilbertTrendline),
            (d, _) =>
            {
                var native = new QuanTAlib.Htit();
                return new(
                    0,
                    d.Closes.Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                        .ToArray()
                );
            },
            (d, _) => Owned(d.IndicatorBars),
            (d, _) => new(0, Reference(d.Closes)),
            MinimumInputCount: 0,
            CompetitorReference: (d, _) => new(0, NativeReference(d.Closes)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(Bar[] bars)
    {
        var indicator = new OldestFirstHilbertTrendline();
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(double[] prices)
    {
        var periods = new double[prices.Length];
        var smoothed = new double[prices.Length];
        var means = new double[prices.Length];
        var result = prices.ToArray();
        for (var i = 0; i < prices.Length; i++)
        {
            means[i] = prices[i];
            if (i >= 5)
            {
                periods[i] = Add(Multiply(.2, 6), Multiply(.8, periods[i - 1]));
                smoothed[i] = Add(Multiply(.33, periods[i]), Multiply(.67, smoothed[i - 2]));
                var cycle = (int)Add(smoothed[i], .5);
                if (cycle > 0)
                {
                    var selected = prices.Skip(Math.Max(0, i - 6)).Take(Math.Min(cycle, i + 1));
                    means[i] = Round(
                        selected.Aggregate(BigInteger.Zero, (n, v) => n + Units(v)),
                        Grid * cycle
                    );
                }
            }
            if (i >= 10)
                result[i] = Round(
                    Enumerable
                        .Range(0, 4)
                        .Aggregate(
                            BigInteger.Zero,
                            (n, j) => n + (4 - j) * Units(means[i - 3 + j])
                        ),
                    10 * Grid
                );
        }
        return result;
    }

    // Full native phasor model independently checks the zero-imaginary reduction
    // used by production. Plain lists model native oldest-first circular indexing.
    internal static double[] NativeReference(double[] prices)
    {
        var buffers = Enumerable.Range(0, 12).Select(_ => new List<double>()).ToArray();
        var capacities = new[] { 7, 7, 7, 7, 7, 2, 2, 2, 2, 2, 2, 4 };
        void Push(int id, double value)
        {
            if (buffers[id].Count == capacities[id])
                buffers[id].RemoveAt(0);
            buffers[id].Add(value);
        }
        double At(int id, int offset) => buffers[id][Math.Min(offset, buffers[id].Count - 1)];
        double Projection(int id, double adjustment) =>
            ((.0962 * At(id, 0) + .5769 * At(id, 2)) - .5769 * At(id, 4) - .0962 * At(id, 6))
            * adjustment;
        var result = new double[prices.Length];
        var last = 0d;
        for (var i = 0; i < prices.Length; i++)
        {
            Push(0, prices[i]);
            if (i < 5)
            {
                for (var j = 1; j < 11; j++)
                    Push(j, 0);
                Push(11, prices[i]);
                result[i] = prices[i];
                continue;
            }
            var adjustment = .075 * last + .54;
            Push(1, (4 * At(0, 0) + 3 * At(0, 1) + 2 * At(0, 2) + At(0, 3)) / 10);
            Push(2, Projection(1, adjustment));
            var q1 = Projection(2, adjustment);
            Push(4, q1);
            var i1 = At(2, 3);
            Push(3, i1);
            var advanceI = Projection(3, adjustment);
            var advanceQ = Projection(4, adjustment);
            var i2 = .2 * (i1 - advanceQ) + .8 * At(5, 0);
            var q2 = .2 * (q1 + advanceI) + .8 * At(6, 0);
            Push(5, i2);
            Push(6, q2);
            var re = .2 * (i2 * At(5, 1) + q2 * At(6, 1)) + .8 * At(7, 0);
            var im = .2 * (i2 * At(6, 1) - q2 * At(5, 1)) + .8 * At(8, 0);
            Push(7, re);
            Push(8, im);
            var period = im != 0 && re != 0 ? 2 * Math.PI / Math.Atan(im / re) : 0;
            period = Math.Min(period, 1.5 * last);
            period = Math.Max(period, .67 * last);
            period = Math.Clamp(period, 6, 50);
            period = .2 * period + .8 * last;
            Push(9, period);
            var smooth = .33 * period + .67 * At(10, 0);
            Push(10, smooth);
            var cycle = (int)(smooth + .5);
            var sum = buffers[0]
                .Take(Math.Min(cycle, buffers[0].Count))
                .Aggregate(0d, (a, b) => a + b);
            Push(11, cycle > 0 ? sum / cycle : prices[i]);
            last = period;
            result[i] =
                i >= 10
                    ? (4 * At(11, 0) + 3 * At(11, 1) + 2 * At(11, 2) + At(11, 3)) / 10
                    : prices[i];
        }
        return result;
    }
}
