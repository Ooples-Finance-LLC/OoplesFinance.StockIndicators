using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactUlcerTests
{
    [Fact]
    public void PrefixRepairMatchesIndependentWindowsForEverySmallPricePattern()
    {
        double[] alphabet = [-1, 0, 1, 2];
        foreach (int period in new[] { 2, 3, 4 })
        for (int pattern = 0; pattern < 1024; pattern++)
        {
            int encoded = pattern;
            var bars = new Bar[5];
            for (int i = 0; i < bars.Length; i++)
            {
                double price = alphabet[encoded & 3]; encoded >>= 2;
                bars[i] = new Bar(default, price, price, price, price, 0);
            }
            var expected = WindowUlcerReference.Calculate(bars, period);
            var state = new WindowUlcerIndex.State(period);
            var output = new double[2];
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(bars[i], output);
                Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public void CachedContributionsFollowExpiringPeaksAbsentPrefixesAndExtremeRecovery(int period)
    {
        double[] extremes = [double.Epsilon, 2 * double.Epsilon, 0, -1, double.MaxValue,
            -double.MaxValue, 1, -double.MaxValue / 80, 1, 1, 0, 1, 1, 1, 100, 90];
        var prices = Enumerable.Range(0, 160).Select(i => i < extremes.Length ? extremes[i]
            : i < 64 ? 100d - i // Repeated expiration of the highest peak.
            : i < 112 ? i % 11 == 0 ? 0d : 50d // Plateau and missing-prefix recovery.
            : 100d + (i * 13 % 101) / 8d).ToArray();
        var state = (IMultiOutputState)new WindowUlcerIndex(period).CreateState();
        var output = new double[2];
        foreach (var sequence in new[] { prices, prices.Reverse().ToArray() })
        {
            state.Reset();
            var bars = sequence.Select(p => new Bar(default, p, p, p, p, 0)).ToArray();
            var expected = WindowUlcerReference.Calculate(bars, period);
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(bars[i], output);
                Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
            }
        }
    }

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
