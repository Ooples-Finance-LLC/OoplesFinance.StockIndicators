using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonCompactInputTests
{
    [Theory]
    [InlineData(TillsonSeed.FirstPrice)]
    [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public void DirectAndMixedInputsMatchWideEntryAcrossReset(TillsonSeed seed)
    {
        double[] boundaries = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), Math.BitDecrement(Math.ScaleB(1, -1022)),
            1, Math.BitIncrement(1), Math.BitDecrement(1), -1,
            double.MaxValue, -double.MaxValue];
        foreach (int period in new[] { 1, 3, 26, int.MaxValue })
        foreach (double factor in new[] { 0d, .7, -1d })
        {
            var owner = new TillsonAverage(period, factor, seed);
            var direct = (IMultiOutputState)owner.CreateState();
            var mixed = (IMultiOutputState)owner.CreateState();
            var wide = (IMultiOutputState)owner.CreateState();
            var actual = new double[2];
            var mixedOutput = new double[2];
            var expected = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                direct.Reset(); mixed.Reset(); wide.Reset();
                for (int i = 0; i < 192; i++)
                {
                    double price = i < 96 ? 100 + i * .125 : boundaries[i % boundaries.Length];
                    var bar = new Bar(default, price, price, price, price, 1);
                    var units = ExactVarianceWindow.Units(price);
                    direct.Update(in bar, actual);
                    if ((i & 1) == 0) mixed.Update(in bar, mixedOutput);
                    else ((IWideAverageState)mixed).UpdateUnits(units, mixedOutput);
                    ((IWideAverageState)wide).UpdateUnits(units, expected);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot]),
                            BitConverter.DoubleToInt64Bits(actual[slot]));
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot]),
                            BitConverter.DoubleToInt64Bits(mixedOutput[slot]));
                    }
                    Assert.Equal(((IWideAverageState)wide).RoundedUnits, ((IWideAverageState)direct).RoundedUnits);
                    Assert.Equal(((IWideAverageState)wide).RoundedUnits, ((IWideAverageState)mixed).RoundedUnits);
                }
            }
        }
    }
}
