using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CertifiedCommodityChannelTests
{
    [Fact]
    public void AcceptedWindowSumsMatchIndependentExactRatios()
    {
        var random = new Random(829731);
        foreach (int count in new[] { 1, 2, 3, 20, 64, 256 })
        foreach (int exponent in new[] { -1000, -500, 0, 500, 1000 })
        foreach (int sign in new[] { -1, 1 })
        for (int replay = 0; replay < 12; replay++)
        {
            var prices = new Queue<double>(Enumerable.Range(0, count)
                .Select(_ => sign * Math.ScaleB(100 + random.NextDouble() * 10, exponent)));
            var sum = new ExactMeanAccumulator();
            foreach (double price in prices) sum.Add(price);
            double mean = sum.Mean(count), current = prices.Last();
            Assert.True(CertifiedCommodityChannel.TryCompute(prices, mean, current, out double actual, out bool defined));
            var expected = ExactRatio(prices, mean, current);
            Assert.Equal(expected.HasValue, defined);
            Equal(expected ?? 0, actual);
        }
    }

    [Fact]
    public void FullUnsignedSumSupportsOppositeSignsAndExtremeNormalPrices()
    {
        foreach (double scale in new[] { 1d, Math.ScaleB(1d, 1000) })
        {
            var prices = new Queue<double>(Enumerable.Range(0, 256)
                .Select(i => scale * (i == 255 ? 4.0000000000000009 : -7.9999999999999991)));
            // Exercise the entire signed alignment range with an independently
            // supplied center; the helper's bounds do not assume positive prices.
            double mean = scale * 3.9999999999999996, current = prices.Last();
            Assert.True(CertifiedCommodityChannel.TryCompute(prices, mean, current, out double actual, out bool defined));
            Assert.True(defined);
            Equal(ExactRatio(prices, mean, current)!.Value, actual);
        }
    }

    [Fact]
    public void FlatWindowsAndUncertainRangesHaveExplicitOutcomes()
    {
        Assert.True(CertifiedCommodityChannel.TryCompute(new Queue<double>([2d, 2, 2]), 2, 2, out double value, out bool defined));
        Equal(0, value); Assert.False(defined);
        Assert.True(CertifiedCommodityChannel.TryCompute(new Queue<double>([1d, 2, 3]), 2, 2, out value, out defined));
        Equal(0, value); Assert.True(defined);
        Assert.False(CertifiedCommodityChannel.TryCompute(new Queue<double>(), 1, 1, out _, out _));
        Assert.False(CertifiedCommodityChannel.TryCompute(new Queue<double>(Enumerable.Repeat(1d, 257)), 1, 1, out _, out _));
        Assert.False(CertifiedCommodityChannel.TryCompute(new Queue<double>([1d, double.Epsilon]), 1, 1, out _, out _));
        Assert.False(CertifiedCommodityChannel.TryCompute(new Queue<double>([1d, 1024]), 2, 1, out _, out _));
        Assert.False(CertifiedCommodityChannel.TryCompute(new Queue<double>([-1d, 1]), 0, 1, out _, out _));
    }

    [Theory]
    [InlineData(20, false)] [InlineData(20, true)]
    [InlineData(256, false)] [InlineData(256, true)]
    [InlineData(257, false)] [InlineData(257, true)]
    public void WindowBoundaryAndResetMatchIndependentFractionReference(int period, bool flatZero)
    {
        var bars = Enumerable.Range(0, period + 40).Select(i =>
        {
            double price = i < period + 2 ? 100 : 100 + (i * 13 % 101) / 8d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
        }).ToArray();
        var expected = CommodityChannelReference.Calculate(bars, period, false, flatZero);
        var state = (IMultiOutputState)new WindowCommodityChannelIndex(period, flatZero: flatZero).CreateState();
        var output = new double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(bars[i], output);
                Equal(expected[i] ?? 0, output[0]);
                Equal(expected[i].HasValue ? 1 : 0, output[1]);
            }
        }
    }

    private static double? ExactRatio(Queue<double> prices, double mean, double current)
    {
        var numerator = new ExactMeanAccumulator();
        numerator.Add(current); numerator.Add(mean, -1);
        numerator.Multiply(200L * prices.Count);
        var denominator = new ExactMeanAccumulator();
        foreach (double price in prices)
        {
            var distance = new ExactMeanAccumulator();
            distance.Add(price); distance.Add(mean, -1);
            if (distance.Sign < 0) distance.Multiply(-1);
            denominator.AddExact(distance);
        }
        if (denominator.Sign == 0) return null;
        denominator.Multiply(3);
        return numerator.Ratio(denominator);
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
