using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SeededAtrTrailingExecutionTests
{
    [Theory]
    [InlineData(2, 3)]
    [InlineData(14, .125)]
    [InlineData(2, double.Epsilon)]
    [InlineData(2, double.MaxValue)]
    [InlineData(int.MaxValue, 3)]
    public void CompactBandsPreserveReferenceBitsTiesAndReset(int period, double multiplier)
    {
        var ordinary = Enumerable.Range(0, 81).Select(i => new Bar(default, 0,
            100 + (i * 13 % 101) / 8d, 96 + (i * 13 % 101) / 8d, 98 + (i * 13 % 101) / 8d, 1)).ToArray();
        Bar[] extremes =
        [
            new(default, 0, double.MaxValue, -double.MaxValue, 1, 1),
            new(default, 0, 1, -1, -double.MaxValue, 1),
            new(default, 0, double.MaxValue, double.MaxValue, double.MaxValue, 1),
            new(default, 0, -5, 7, 0, 1),
            new(default, 0, double.Epsilon, -double.Epsilon, double.Epsilon, 1),
            new(default, 0, -0d, 0d, -0d, 1)
        ];
        var flat = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 0), 35).ToArray();
        foreach (var basis in Enum.GetValues<AtrTrailBasis>())
        {
            var state = (IMultiOutputState)new SeededAtrTrailingStop(period, multiplier, basis).CreateState();
            var output = new double[6];
            foreach (var bars in new[] { ordinary, extremes.Concat(ordinary).ToArray(),
                extremes.Reverse().Concat(ordinary).ToArray(), flat, ordinary })
            {
                var expected = AtrTrailingReference.Values(bars, period, multiplier, basis);
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 3]);
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                            BitConverter.DoubleToInt64Bits(output[slot]));
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(AtrTrailBasis.Close)]
    [InlineData(AtrTrailBasis.HighLow)]
    [InlineData(AtrTrailBasis.Midpoint)]
    public void OrdinaryUpdatesIncludingResetStartupAllocateNoPerBarObjects(AtrTrailBasis basis)
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
            100 + i / 8d, 98 + i / 8d, 99 + i / 8d, 1)).ToArray();
        var state = (IMultiOutputState)new SeededAtrTrailingStop(14, 3, basis).CreateState();
        var output = new double[6];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        state.Reset();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[3]);
        Assert.Equal(1d, output[4] + output[5]);
    }
}
