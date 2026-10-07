using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WindowModeComparison
{
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Mode",
        "WindowMode",
        Native,
        Owned,
        (d, p) => Reference(d, p, false),
        CompetitorReference: (d, p) => Reference(d, p, true)
    );

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Mode(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(CompetitorData data, int period)
    {
        var indicator = new WindowMode(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var result = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var count = Math.Min(period, i + 1);
            var values = data.Closes.Skip(i - count + 1).Take(count).ToArray();
            if (count == period)
                values = Modes(values);
            if (native)
            {
                var sum = count == period ? values.Aggregate(0d, Add) : NativeStartupSum(values);
                result[i] = Divide(sum, values.Length);
            }
            else
                result[i] = Round(
                    values.Aggregate(BigInteger.Zero, (sum, value) => sum + Units(value)),
                    Grid * values.Length
                );
        }
        return new(0, result);
    }

    private static double[] Modes(double[] values)
    {
        // Batch sorting/run-length encoding is independent of production's
        // evicting frequency dictionary.
        Array.Sort(values);
        var modes = new List<double>();
        var maxCount = 0;
        for (var start = 0; start < values.Length; )
        {
            var end = start + 1;
            while (end < values.Length && values[end].Equals(values[start])) // NOSONAR: Modes count exact binary64 values; a tolerance would merge distinct observations.
                end++;
            var frequency = end - start;
            if (frequency > maxCount)
            {
                modes.Clear();
                maxCount = frequency;
            }
            if (frequency == maxCount)
                modes.Add(values[start]);
            start = end;
        }
        return modes.ToArray();
    }

    private static double NativeStartupSum(double[] values)
    {
        // Model lane ordering without calling the package's buffer or SIMD arithmetic.
        var width = Vector<double>.Count;
        var lanes = new double[width];
        var vectorEnd = values.Length / width * width;
        for (var i = 0; i < vectorEnd; i++)
            lanes[i % width] = Add(lanes[i % width], values[i]);
        var sum = lanes.Aggregate(0d, Add);
        for (var i = vectorEnd; i < values.Length; i++)
            sum = Add(sum, values[i]);
        return sum;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 1, 9, 3, 3, 9, 9, -1, -1, 3, -1, 9]);

    internal static CompetitorData AdjacentFixture() =>
        CompetitorData.FromCloses([
            .1,
            Math.BitIncrement(.1),
            .1,
            Math.BitIncrement(.1),
            Math.BitIncrement(.1),
            .1,
        ]);

    internal static CompetitorData LaneFixture() =>
        CompetitorData.FromCloses([1e16, 1, -1e16, 1, 1, 1, 1, 1]);
}
