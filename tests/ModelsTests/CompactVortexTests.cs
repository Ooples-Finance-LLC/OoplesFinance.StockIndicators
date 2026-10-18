using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactVortexTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(14)] [InlineData(int.MaxValue)]
    public void ExactDifferencesPreserveExtremesInvertedCandlesAndReset(int period)
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            -double.MaxValue, 1, Math.BitIncrement(1d), -1, Math.BitDecrement(-1d)];
        var bars = Enumerable.Range(0, 90).Select(i => new Bar(default, 0,
            values[i % values.Length], values[(i + 3) % values.Length], values[(i + 7) % values.Length], 0))
            .Concat(Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 0), 30)).ToArray();
        var expected = TrueRangeRatioReference.Vortex(bars, period);
        var state = (IMultiOutputState)new WindowVortex(period).CreateState();
        Span<double> output = stackalloc double[4];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 4; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(output[slot]));
            }
        }
    }

    [Fact]
    public void WarmedOrdinaryWindowsAllocateNoPerBarObjects()
    {
        var state = (IMultiOutputState)new WindowVortex(14).CreateState();
        Span<double> output = stackalloc double[4];
        for (int replay = 0; replay < 2; replay++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            state.Reset();
            for (int i = 0; i < 1000; i++)
            {
                double value = 10 + (i * 13 % 101) / 8d;
                var bar = new Bar(default, value, value + 1, value - 1, value, 1);
                state.Update(in bar, output);
            }
            if (replay > 0) Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.True(double.IsFinite(output[0]));
        Assert.Equal(1d, output[2]);
    }
}
