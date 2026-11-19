using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AdxWeightedCertificateTests
{
    [Fact]
    public void AcceptedCellsMatchIndependentExactAccumulator()
    {
        int accepted = 0, fallback = 0;
        var random = new Random(72631);
        int[] periods = [2, 3, 14, 31, 2047, int.MaxValue];
        for (int i = 0; i < 12000; i++)
        {
            int period = periods[i % periods.Length];
            double prior = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-500, 501));
            double input = i % 3 == 0 ? Math.BitIncrement(prior)
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-500, 501));
            foreach (int weight in new[] { 1, period })
            {
                if (!SumSeededDirectionalIndex.WeightedCertificate.TrySum(prior, input, period, weight, out var actual))
                {
                    fallback++;
                    continue;
                }
                var exact = new ExactMeanAccumulator();
                exact.Add(prior, period - 1);
                exact.Add(input, weight);
                Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Mean(period)), BitConverter.DoubleToInt64Bits(actual));
                accepted++;
            }
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
    }

    [Fact]
    public void TiesAndUnsupportedRangesUseExactFallback()
    {
        // The exact average lies halfway between 1 and its successor.
        Assert.False(SumSeededDirectionalIndex.WeightedCertificate.TrySum(1, Math.BitIncrement(1), 2, 1, out _));
        foreach (double value in new[] { double.Epsilon, Math.ScaleB(1, -500), Math.ScaleB(1, 451), double.MaxValue, double.PositiveInfinity, double.NaN })
            Assert.False(SumSeededDirectionalIndex.WeightedCertificate.TrySum(value, value, 14, 14, out _));
        Assert.False(SumSeededDirectionalIndex.WeightedCertificate.TrySum(0, 0, 14, 1, out _));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(31)]
    public void CompleteTrajectoriesRetainEveryRoundedStageAndReset(int period)
    {
        var random = new Random(731);
        var bars = Enumerable.Range(0, 700).Select(i =>
        {
            double center = 100 + random.NextDouble() * 30;
            return new Bar(default, center, center + random.NextDouble() * 10,
                center - random.NextDouble() * 10, center, 1);
        }).ToArray();
        var state = (IMultiOutputState)new SumSeededDirectionalIndex(period).CreateState();
        var output = new double[10];
        foreach (int exponent in new[] { 0, -1020, 1016, 0 })
        {
            var scaled = bars.Select(b => new Bar(default, Math.ScaleB(b.Open, exponent),
                Math.ScaleB(b.High, exponent), Math.ScaleB(b.Low, exponent), Math.ScaleB(b.Close, exponent), 1)).ToArray();
            var expected = SumDirectionalReference.Calculate(scaled, period);
            state.Reset();
            for (int i = 0; i < scaled.Length; i++)
            {
                state.Update(in scaled[i], output);
                for (int slot = 0; slot < 5; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 5]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0), BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }
}
