using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactPhaseAdaptiveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void OrdinaryAdaptiveStagesAllocateNothingIncludingStartup(int configuration)
    {
        MultiOutputIndicatorBase indicator = configuration switch
        {
            0 => new SeededPhaseAdaptiveAverage(),
            1 => new SeededPhaseAdaptiveAverage(publishStartup: true),
            2 => new SeededPhaseAdaptiveAverage(retainOlderZeroPhase: true),
            3 => new SeededPhaseAdaptiveAverage(useMidpoint: true),
            4 => new DelayedPhaseAdaptiveAverage(),
            _ => new DelayedPhaseAdaptiveAverage(.1, .1, 7)
        };
        var state = (IMultiOutputState)indicator.CreateState();
        var output = new double[4];
        void Run()
        {
            for (int i = 0; i < 4000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                state.Update(new Bar(default, price, price + 1, price - 1, price, 1), output);
            }
        }
        // Warm the fused stages through their tiered compilation transition;
        // retain startup and reset in the measured replay.
        for (int warmup = 0; warmup < 3; warmup++) { state.Reset(); Run(); }
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), output.Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(.1)]
    [InlineData(1d)]
    [InlineData(double.Epsilon)]
    public void ConstantGainWideCompositionPreservesExactComplementAndStageRounding(double alpha)
    {
        // Equal limits make the gain constant independently of the Hilbert
        // control. Compare both stages with the original exact integer formula.
        var state = (IWideAverageState)new SeededPhaseAdaptiveAverage(alpha, alpha, publishStartup: true).CreateState();
        BigInteger grid = BigInteger.One << 1074;
        BigInteger mother = 0, following = 0, seed = 0;
        BigInteger gain = ExactVarianceWindow.Units(alpha), followingGain = ExactVarianceWindow.Units(.5 * alpha);
        BigInteger huge = ExactVarianceWindow.Units(double.MaxValue) << 64;
        BigInteger[] prices = [ExactVarianceWindow.Units(100), ExactVarianceWindow.Units(101),
            ExactVarianceWindow.Units(99), ExactVarianceWindow.Units(102), ExactVarianceWindow.Units(98),
            ExactVarianceWindow.Units(103), huge, -huge, 1, -1, 0, ExactVarianceWindow.Units(101)];
        var output = new double[4];
        for (int i = 0; i < 2 * prices.Length; i++)
        {
            var price = prices[i % prices.Length];
            if (i < 6)
            {
                seed += price;
                mother = following = RocBankValue.RoundUnits(seed, i + 1);
            }
            else
            {
                mother = RocBankValue.RoundUnits(price * gain + mother * (grid - gain), grid);
                following = RocBankValue.RoundUnits(mother * followingGain + following * (grid - followingGain), grid);
            }
            state.UpdateUnits(price, output);
            Assert.Equal(mother, state.RoundedUnits);
            Assert.Equal(BitConverter.DoubleToInt64Bits(ExactMeanAccumulator.UnitRatio(mother, 1)),
                BitConverter.DoubleToInt64Bits(output[0]));
            Assert.Equal(BitConverter.DoubleToInt64Bits(ExactMeanAccumulator.UnitRatio(following, 1)),
                BitConverter.DoubleToInt64Bits(output[1]));
            Assert.Equal(1d, output[2]);
            Assert.Equal(1d, output[3]);
        }
    }
}
