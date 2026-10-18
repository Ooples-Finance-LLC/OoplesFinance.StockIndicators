using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedFisherStateTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void CompactStateMatchesIndependentReferenceAcrossWindowsAndResets(int period)
    {
        double[][] inputs =
        [
            Enumerable.Range(0, 160).Select(i => (i * 17 % 61 - 30) / 8d).ToArray(),
            Enumerable.Repeat(7d, 30).Concat(Enumerable.Range(0, 60).Select(i => i / 8d)).ToArray(),
            [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1, -1],
            Enumerable.Range(0, 100).Select(i => Math.ScaleB(1 + i / 128d, i < 30 ? 700 : -700)).ToArray()
        ];
        foreach (bool midpoint in new[] { false, true })
        {
            var state = (IMultiOutputState)new WindowFisherTransform(period, midpoint).CreateState();
            var actual = new double[4];
            foreach (var prices in inputs)
            {
                state.Reset();
                var bars = prices.Select((v, i) => new Bar(default, 0, v,
                    midpoint && i % 3 == 0 ? v / 2 : v, v, 1)).ToArray();
                var expected = WindowFisherReference.Values(bars, period, midpoint);
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], actual);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0), BitConverter.DoubleToInt64Bits(actual[slot]));
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, actual[slot + 2]);
                    }
                }
            }
        }
    }

    [Fact]
    public void WarmedOrdinaryUpdatesIncludingResetStartupAllocateNoManagedBytes()
    {
        var state = (IMultiOutputState)new WindowFisherTransform(20).CreateState();
        var bars = Enumerable.Range(0, 1000).Select(i => new Bar(default, 0,
            10 + (i % 101) / 8d, 9 + (i % 101) / 8d, 0, 1)).ToArray();
        var output = new double[4];
        foreach (var bar in bars) state.Update(in bar, output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        state.Reset();
        foreach (var bar in bars) state.Update(in bar, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
