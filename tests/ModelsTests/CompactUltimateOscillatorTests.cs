using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactUltimateOscillatorTests
{
    [Theory]
    [InlineData(1, 2, 3, false)] [InlineData(7, 14, 28, false)]
    [InlineData(1, 1, 1, true)] [InlineData(4, 2, 4, true)]
    [InlineData(1, 2, int.MaxValue, false)] [InlineData(3, 2, int.MaxValue, true)]
    public void WeightedRatiosPreserveExactRoundingExtremesZeroPoliciesAndReset(int a, int b, int c, bool zero)
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            -double.MaxValue, 1, Math.BitIncrement(1d), -1, Math.BitDecrement(-1d)];
        var bars = Enumerable.Range(0, 60).Select(i => new Bar(default, 0,
            values[i % values.Length], values[(i + 3) % values.Length], values[(i + 7) % values.Length], 0))
            .Concat(Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 0), 35)).ToArray();
        var indicator = new WindowUltimateOscillator(a, b, c, zero);
        var expected = TrueRangeRatioReference.Ultimate(bars,
            [indicator.ShortPeriod, indicator.MiddlePeriod, indicator.LongPeriod], zero);
        var state = (IMultiOutputState)indicator.CreateState();
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
    public void WarmedOrdinaryWindowsAllocateNoPerBarObjects(bool zero)
    {
        var state = (IMultiOutputState)new WindowUltimateOscillator(7, 14, 28, zero).CreateState();
        Span<double> output = stackalloc double[2];
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
        Assert.True(double.IsFinite(output[0])); Assert.Equal(1d, output[1]);
    }
}
