using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactCommodityChannelTests
{
    [Theory]
    [InlineData(1, false, false)] [InlineData(1, true, true)]
    [InlineData(7, false, false)] [InlineData(7, false, true)]
    [InlineData(7, true, false)] [InlineData(7, true, true)]
    [InlineData(int.MaxValue, false, false)] [InlineData(int.MaxValue, true, true)]
    public void ExactWindowsPreserveResetFlatStartupAndWideFallback(int period, bool rolling, bool flatZero)
    {
        double[] extremes = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon,
            0, -0d, 1, Math.BitIncrement(1d), Math.BitDecrement(1d)];
        var bars = extremes.Concat(Enumerable.Range(0, 37).Select(i => (i * 13 % 101 - 50) / 8d))
            .Concat(Enumerable.Repeat(1d, 20)).Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var expected = CommodityChannelReference.Calculate(bars, period, rolling, flatZero).ToArray();
        var state = (IMultiOutputState)new WindowCommodityChannelIndex(period, rolling, flatZero).CreateState();
        Span<double> output = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WarmedOrdinaryWindowsAllocateNoPerBarObjects(bool rolling)
    {
        var state = (IMultiOutputState)new WindowCommodityChannelIndex(20, rolling).CreateState();
        Span<double> output = stackalloc double[2];
        for (int i = 0; i < 100; i++)
        {
            double value = (i * 13 % 101 - 50) / 8d;
            var bar = new Bar(default, value, value + 1, value - 1, value, 1);
            state.Update(in bar, output);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        state.Reset();
        for (int i = 0; i < 1000; i++)
        {
            double value = (i * 13 % 101 - 50) / 8d;
            var bar = new Bar(default, value, value + 1, value - 1, value, 1);
            state.Update(in bar, output);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(output[0]));
        Assert.Equal(1d, output[1]);
    }
}
