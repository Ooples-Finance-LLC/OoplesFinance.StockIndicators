using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SeededPriceMomentumExecutionTests
{
    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(7, 3, 5)]
    [InlineData(int.MaxValue, 1, 2)]
    [InlineData(2, int.MaxValue, 2)]
    [InlineData(2, 1, int.MaxValue)]
    public void CompactStagesPreserveReferenceBitsAndReset(int period, int smooth, int signal)
    {
        var ordinary = Enumerable.Range(0, 81).Select(i => 100 + (i * 17 % 31) / 8d).ToArray();
        double[] extremes = [double.Epsilon, double.MaxValue, -double.MaxValue, double.Epsilon,
            -double.Epsilon, Math.BitIncrement(1), -1, double.MaxValue];
        var missingSeed = new[] { 1d, 0, 2, 3, 4 }.Concat(ordinary).ToArray();
        var missingLater = ordinary.Concat(new[] { 0d, 1d }).Concat(ordinary).ToArray();
        var state = (IMultiOutputState)new SeededPriceMomentum(period, smooth, signal).CreateState();
        var output = new double[4];
        foreach (var prices in new[] { ordinary, extremes.Concat(ordinary).ToArray(),
            extremes.Reverse().Concat(ordinary).ToArray(), missingSeed, missingLater,
            Enumerable.Repeat(1d, 40).ToArray(), ordinary })
        {
            var bars = prices.Select(v => new Bar(default, v, v, v, v, 0)).ToArray();
            var expected = SeededMomentumReference.Pmo(bars, period, smooth, signal);
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 2]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }

    [Fact]
    public void OrdinaryStreamingUpdatesIncludingResetStartupAllocateNoPerBarObjects()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 0, 0,
            100 + (i * 13 % 101) / 8d, 1)).ToArray();
        var state = (IMultiOutputState)new SeededPriceMomentum().CreateState();
        var output = new double[4];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        state.Reset();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[2]);
        Assert.Equal(1d, output[3]);
    }
}
