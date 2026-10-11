using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactHilbertPhaseTests
{
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
        Run();
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
