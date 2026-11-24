using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DeferredRegressionMomentTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void DeferredFitsMatchEveryBarStateAcrossEvictionsAndReset(int period)
    {
        double[][] fixtures = [
            Enumerable.Range(0, 90).Select(i => 10 + (i * 13 % 101) / 8d).ToArray(),
            [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, -0d, 1, -1],
            Enumerable.Range(0, 90).Select(i => Math.ScaleB(i % 17 - 8, i % 3 == 0 ? 700 : -700)).ToArray()
        ];
        using var every = new WindowRegressionStatistics.State(period);
        using var deferred = new WindowRegressionStatistics.State(period);
        var expected = new double[10];
        var actual = new double[10];
        foreach (var prices in fixtures)
        {
            every.Reset(); deferred.Reset();
            for (int i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                every.Update(bar, expected);
                if (i % 7 != 6 && i != prices.Length - 1)
                {
                    deferred.Accumulate(bar);
                    continue;
                }
                deferred.Update(bar, actual);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(BitConverter.DoubleToInt64Bits(every.GlobalIntercept), BitConverter.DoubleToInt64Bits(deferred.GlobalIntercept));
                foreach (int position in new[] { 0, Math.Min(period, i + 1) - 1 })
                    Assert.Equal(BitConverter.DoubleToInt64Bits(every.LineAt(position)), BitConverter.DoubleToInt64Bits(deferred.LineAt(position)));
            }
        }
    }
}
