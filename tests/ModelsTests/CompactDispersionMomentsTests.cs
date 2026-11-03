using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactDispersionMomentsTests
{
    [Fact]
    public void RollingMomentsMatchIndependentSubnormalGridThroughRescalingAndPromotion()
    {
        double[][] sequences = [
            Enumerable.Range(0, 96).Select(i => 123.45 + (i % 17 - 8) * .03125).ToArray(),
            [0, -0d, 8, -8, .5, -.5, .03125, -.03125, double.Epsilon,
                -double.Epsilon, double.MaxValue, -double.MaxValue, 1, 2, 3, 4, 5, 6, 7, 8],
            Enumerable.Range(0, 40).Select(i => Math.ScaleB((i % 7 - 3) / 8d,
                i < 10 ? 500 : i < 20 ? -500 : i < 30 ? 20 : -1070)).ToArray()
        ];
        foreach (var values in sequences)
        foreach (int period in new[] { 2, 7, 20 })
        foreach (bool sample in new[] { false, true })
        foreach (double multiplier in new[] { 1d, -.3, Math.ScaleB(1d, -600), Math.ScaleB(-1d, 600) })
        foreach (var selected in Enum.GetValues<WindowDispersionOutput>())
        {
            var state = new WindowDispersion.State(period, selected, sample, multiplier);
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                var window = new Queue<BigInteger>();
                foreach (double value in values)
                {
                    if (window.Count == period) window.Dequeue();
                    var current = ExactVarianceWindow.Units(value);
                    window.Enqueue(current);
                    int n = window.Count;
                    var sum = window.Aggregate(BigInteger.Zero, (a, b) => a + b);
                    var squares = window.Aggregate(BigInteger.Zero, (a, b) => a + b * b);
                    var variance = n * squares - sum * sum;
                    double expected = Reference(selected, sample, multiplier, current, sum, variance, n);
                    double actual = state.Update(new Bar(default, value, value, value, value, 1));
                    AssertBits(expected, actual);
                    AssertBits(ExactMeanAccumulator.UnitRatio(sum, n), state.Mean);
                    Assert.Equal(n > 1 && variance.Sign > 0, state.HasVariance);
                }
            }
        }
    }

    private static double Reference(WindowDispersionOutput selected, bool sample, double multiplier,
        BigInteger current, BigInteger sum, BigInteger variance, int n)
    {
        if (n < 2) return 0;
        var scale = ExactMeanAccumulator.DecomposeFinite(multiplier);
        var denominator = new BigInteger(n) * (sample ? n - 1 : n);
        if (selected == WindowDispersionOutput.Variance)
            return ExactMeanAccumulator.ScaledRatio(variance * scale.Integer, denominator, -2148 + scale.Exponent);
        if (selected == WindowDispersionOutput.StandardDeviation)
            return Math.Sign(scale.Integer) * ExactPopulationDeviation.ScaledRootRatio(
                variance * scale.Integer * scale.Integer, denominator, -2148 + 2 * scale.Exponent);
        if (variance.IsZero) return 0;
        var deviation = (n * current - sum) * scale.Integer;
        return deviation.Sign * ExactPopulationDeviation.ScaledRootRatio(
            deviation * deviation * denominator, new BigInteger(n) * n * variance, 2 * scale.Exponent);
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
