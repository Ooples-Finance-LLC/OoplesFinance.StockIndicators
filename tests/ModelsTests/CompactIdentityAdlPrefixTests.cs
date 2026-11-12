using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactIdentityAdlPrefixTests
{
    [Fact]
    public void MixedGridsAndWideCancellationMatchIndependentRationalHistoryAfterReset()
    {
        double[] volumes = [1, Math.ScaleB(1, 64), .1, Math.ScaleB(1, -70),
            Math.ScaleB(1, 120), -Math.ScaleB(1, 120), -Math.ScaleB(1, 64),
            -1, -.1, -Math.ScaleB(1, -70), double.Epsilon, -double.Epsilon];
        var bars = Enumerable.Range(0, 240).Select(i =>
            new Bar(default, 0, 3, 1, i % 3 == 0 ? 1 : 3, volumes[i % volumes.Length])).ToArray();
        Check(bars);
    }

    [Fact]
    public void ExtendedFlowsAndOverflowedPrefixesRecoverByExactCancellation()
    {
        Check([
            new(default, 0, 1, 0, 3, double.MaxValue),
            new(default, 0, 1, 0, -2, double.MaxValue),
            new(default, 0, 1, 0, 1, double.MaxValue),
            new(default, 0, 1, 0, 1, double.MaxValue),
            new(default, 0, 1, 0, 0, double.MaxValue),
            new(default, 0, 1, 0, 0, double.MaxValue),
            new(default, 0, 3 * double.Epsilon, double.Epsilon, 2 * double.Epsilon, 1),
            new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, .1),
            new(default, 0, 3, 1, 3, double.Epsilon)
        ]);
    }

    [Fact]
    public void BoundedWidePrefixesAllocateNoManagedScratch()
    {
        var state = new MoneyFlowAccumulationWindow.IdentityBatchState();
        double[] volumes = [Math.ScaleB(1, 64), 1, -Math.ScaleB(1, 64), -1];
        for (int i = 0; i < 1000; i++) state.Next(3, 1, 3, volumes[i % 4]);
        long bits = 0, before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++)
            bits |= BitConverter.DoubleToInt64Bits(state.Next(3, 1, 3, volumes[i % 4]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(0L, bits);
    }

    private static void Check(Bar[] bars)
    {
        var expected = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, new Adl(1))["Adl"];
        var state = new MoneyFlowAccumulationWindow.IdentityBatchState();
        for (int pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                    BitConverter.DoubleToInt64Bits(state.Next(bar.High, bar.Low, bar.Close, bar.Volume)));
            }
        }
    }
}
