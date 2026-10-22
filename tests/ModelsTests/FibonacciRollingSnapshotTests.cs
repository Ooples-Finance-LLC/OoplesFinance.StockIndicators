using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class FibonacciRollingSnapshotTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(14)]
    [InlineData(78)] [InlineData(79)] [InlineData(200)]
    public void RollingWindowsMatchIndependentRescansAcrossNumericalBoundaries(int period)
    {
        var random = new Random(8137);
        var ordinary = Enumerable.Range(0, 2 * period + 57).Select(_ => 100 + random.NextDouble() * 20).ToArray();
        Check(ordinary, period);
        Check(ordinary.Select((v, i) => (i % 2 == 0 ? 1 : -1) * Math.ScaleB(v, i % 3 == 0 ? 900 : -1000)).ToArray(), period);
        Check(Enumerable.Range(0, period + 13).Select(i => (i % 4) switch
        {
            0 => double.MaxValue, 1 => -double.MaxValue, 2 => double.Epsilon, _ => -double.Epsilon
        }).ToArray(), period);
        Check(Enumerable.Repeat(-0d, period + 3).ToArray(), period);
    }

    [Fact]
    public void LongHistoryHasNoRecurrenceDrift()
    {
        var prices = Enumerable.Range(0, 10000).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        Check(prices, 14);
        Check(prices, 78);
    }

    [Fact]
    public void AvailablePrefixAndEmptyInputKeepTheirContracts()
    {
        Check([1, 2, 4, 8], 200);
        Assert.Empty(FibonacciWeightedSnapshot.Calculate(Array.Empty<Bar>(), 14));
        Assert.Throws<ArgumentOutOfRangeException>(() => FibonacciWeightedSnapshot.Calculate(Array.Empty<Bar>(), 0));
    }

    private static void Check(double[] prices, int period)
    {
        var bars = prices.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var actual = FibonacciWeightedSnapshot.Calculate(bars, period);
        var weights = new BigInteger[period];
        weights[0] = 1;
        if (period > 1) weights[1] = 1;
        for (int i = 2; i < period; i++) weights[i] = weights[i - 1] + weights[i - 2];
        for (int i = 0; i < prices.Length; i++)
        {
            BigInteger sum = 0, mass = 0;
            for (int lag = 0; lag < Math.Min(period, i + 1); lag++)
            {
                var weight = weights[period - lag - 1];
                sum += weight * ExactVarianceWindow.Units(prices[i - lag]);
                mass += weight;
            }
            double expected = ExactMeanAccumulator.UnitRatio(sum, mass);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual[i]));
        }
    }
}
