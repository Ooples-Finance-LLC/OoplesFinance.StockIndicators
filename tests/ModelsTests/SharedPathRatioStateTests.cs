using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedPathRatioStateTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void CompactNullablePathMatchesPreviousExactAccumulator(int period)
    {
        var random = new Random(9281);
        double?[][] cases =
        [
            Enumerable.Range(0, 100).Select(i => (double?)(i % 17 / 8d)).ToArray(),
            Enumerable.Range(0, 100).Select(i => i % 13 == 0 ? null : (double?)(random.NextDouble() * 200 - 100)).ToArray(),
            [double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, -0d, 1, 1, 1, 1],
            [1, null, 9, 10, 10, null, 11, 12, 12, 12],
            // Rounded percent products must fall back before dividing; overflowing
            // products can still have finite ratios. Include subnormal residuals.
            [0, Math.BitIncrement(1), 0, Math.BitDecrement(1), -Math.BitIncrement(1), 0],
            [0, double.MaxValue / 2, 0, -double.MaxValue / 2, 0],
            [0, double.Epsilon, 0, -double.Epsilon, double.Epsilon * 3, 0]
        ];
        foreach (var convention in Enum.GetValues<PathRatioConvention>())
        foreach (var values in cases)
        {
            var expected = Legacy(values, period, convention);
            var actual = WindowPathRatio.FromValues(values, period, convention).ToArray();
            Assert.Equal(expected.Select(Bits), actual.Select(Bits));
        }
    }
    private static long? Bits(double? value) => value.HasValue ? BitConverter.DoubleToInt64Bits(value.Value) : null;

    [Theory]
    [InlineData(PathRatioConvention.SignedPercent)]
    [InlineData(PathRatioConvention.AbsoluteFraction)]
    public void WarmedStateIncludingResetStartupAllocatesNoManagedBytes(PathRatioConvention convention)
    {
        var state = (IMultiOutputState)new WindowPathRatio(20, convention).CreateState();
        var bars = Enumerable.Range(0, 1000).Select(i => new Bar(default, 0, 1, 0, (i % 101) / 8d, 1)).ToArray();
        var output = new double[2];
        foreach (var bar in bars) state.Update(in bar, output);
        var expected = output.ToArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        state.Reset();
        foreach (var bar in bars) state.Update(in bar, output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, output);
        Assert.Equal(0, allocated);
    }

    // Freeze the prior accumulator path to detect rounding or nullable-window drift.
    private static double?[] Legacy(double?[] input, int period, PathRatioConvention convention)
    {
        var prices = new Queue<double?>();
        var steps = new Queue<ExactMeanAccumulator>();
        var path = new ExactMeanAccumulator();
        double? previous = null;
        var result = new double?[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            var value = input[i];
            var step = new ExactMeanAccumulator();
            if (value.HasValue && previous.HasValue)
            {
                step.Add(Math.Max(value.Value, previous.Value));
                step.Add(Math.Min(value.Value, previous.Value), -1);
            }
            if (steps.Count == period) path.Subtract(steps.Dequeue());
            steps.Enqueue(step); path.AddExact(step); previous = value;
            bool mature = prices.Count == period;
            var first = mature ? prices.Dequeue() : null;
            prices.Enqueue(value);
            if (!mature || !value.HasValue || !first.HasValue || path.IsExactlyZero) continue;
            var numerator = new ExactMeanAccumulator();
            int scale = convention == PathRatioConvention.SignedPercent ? 100 : 1;
            numerator.Add(value.Value, scale); numerator.Add(first.Value, -scale);
            var ratio = numerator.Ratio(path);
            result[i] = convention == PathRatioConvention.AbsoluteFraction ? Math.Abs(ratio) : ratio;
        }
        return result;
    }
}
