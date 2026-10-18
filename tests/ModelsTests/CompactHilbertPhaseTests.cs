using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactHilbertPhaseTests(Xunit.Abstractions.ITestOutputHelper log)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SignalStatesAvoidPaddedProjectionAllocation(int kind)
    {
        var state = new HilbertCycleSignalState((HilbertCycleSignal)kind, 0);
        var output = new double[kind == 1 ? 4 : 2];
        void Run()
        {
            for (int i = 0; i < 4000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                state.Update(new Bar(default, price, price + 1, price - 1, price, 1), output);
            }
        }
        Run();
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        log.WriteLine($"Hilbert signal kind {kind}: {allocated} bytes over 4000 updates including startup.");
        // Wide exact projection tails remain allowed. Prevent a return to the
        // padded arithmetic's tens of kilobytes of scratch per ordinary bar.
        Assert.InRange(allocated, 0, 64_000_000);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), output.Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompleteTrendlineStatesAllocateNothingIncludingStartup(int kind)
    {
        MultiOutputIndicatorBase indicator = kind == 0 ? new DelayedHilbertTrendline()
            : new SeededHilbertTrendline(useMidpoint: kind == 1);
        var state = (IMultiOutputState)indicator.CreateState();
        var output = new double[indicator.Outputs.Count];
        void Run()
        {
            for (int i = 0; i < 4000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                state.Update(new Bar(default, price, price + 1, price - 1, price, 1), output);
            }
        }
        Run();
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), output.Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(6, false, false)]
    [InlineData(6, false, true)]
    [InlineData(12, true, false)]
    [InlineData(37, true, false)]
    public void OrdinaryPhaseStagesAllocateNothingIncludingStartup(int start, bool retainPeriod, bool retainOlder)
    {
        var state = new HilbertPhaseState(start, retainPeriod, retainOlder);
        void Run()
        {
            for (int i = 0; i < 4000; i++) state.Update(100 + (i * 13 % 101) / 8d);
        }
        // Let tiered compilation settle before counting managed allocations.
        // The measured replay below still starts from a freshly reset state.
        for (int warmup = 0; warmup < 3; warmup++) { state.Reset(); Run(); }
        double period = state.Period, phase = state.Phase;
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(BitConverter.DoubleToInt64Bits(period), BitConverter.DoubleToInt64Bits(state.Period));
        Assert.Equal(BitConverter.DoubleToInt64Bits(phase), BitConverter.DoubleToInt64Bits(state.Phase));
        Assert.True(double.IsFinite(state.InPhase.Mean(1)));
    }

    [Fact]
    public void UnitBridgePreservesCompactWideAndExtendedRounding()
    {
        foreach (int width in new[] { 1, 53, 63, 127, 128, 257 })
        foreach (int shift in new[] { 0, 1000, 1074, 2048, 3000 })
        foreach (int sign in new[] { -1, 1 })
        {
            BigInteger units = sign * (((BigInteger.One << width) - 1) << shift);
            var value = JurikDyadic.FromUnits(units);
            Assert.Equal(RocBankValue.RoundUnits(units, 1), value.RoundedUnits());
            // Cancellation must retain every bit of the imported value, even
            // when the export alone would have rounded its low bits away.
            value.Subtract(JurikDyadic.FromUnits(units - 1));
            Assert.Equal(BigInteger.One, value.RoundedUnits());
        }
        Assert.Equal(BigInteger.Zero, JurikDyadic.FromUnits(0).RoundedUnits());
        foreach (int multiple in new[] { 1, 3, 5 })
        {
            var value = JurikDyadic.FromDouble(multiple * double.Epsilon);
            value.Multiply(.5);
            Assert.Equal(RocBankValue.RoundUnits(multiple, 2), value.RoundedUnits());
        }
    }
}
