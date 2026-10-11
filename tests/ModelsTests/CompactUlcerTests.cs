using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactUlcerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(50)]
    public void OrdinaryWindowsAllocateNothingAfterCapacityWarmup(int period)
    {
        var state = (IMultiOutputState)new WindowUlcerIndex(period).CreateState();
        var output = new double[2];
        void Run()
        {
            for (int i = 0; i < 4000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                state.Update(new Bar(default, price, price, price, price, 0), output);
            }
        }
        for (int warmup = 0; warmup < 3; warmup++) { state.Reset(); Run(); }
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public void CompactAndExtendedStagesMatchOriginalIntegerWindows(int period)
    {
        double[] prices = [1, -double.MaxValue / 80, 1, 1, 0, -1, double.Epsilon,
            2 * double.Epsilon, double.Epsilon, double.MaxValue, -double.MaxValue,
            0, 1, Math.BitDecrement(1d), Math.BitIncrement(1d), 1, -1e153,
            100, 99, 101, 98, 102, 100, 97, 103, 99, 104, 98, 101];
        var state = (IMultiOutputState)new WindowUlcerIndex(period).CreateState();
        var output = new double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < prices.Length; i++)
            {
                double price = prices[i];
                state.Update(new Bar(default, price, price, price, price, 0), output);
                bool present = i >= period - 1;
                BigInteger peak = 0, squares = 0;
                if (present)
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        BigInteger units = ExactVarianceWindow.Units(prices[j]);
                        peak = BigInteger.Max(peak, units);
                        if (peak.IsZero) { present = false; break; }
                        BigInteger drawdown = RocBankValue.RoundUnits(100 * (units - peak) << 1074, peak);
                        squares += drawdown * drawdown;
                    }
                double expected = present ? ExactPopulationDeviation.RootRatio(squares, period) : 0;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(output[0]));
                Assert.Equal(present ? 1d : 0, output[1]);
                if (period == 4 && i == 3) Assert.True(double.IsFinite(output[0]));
            }
        }
    }
}
