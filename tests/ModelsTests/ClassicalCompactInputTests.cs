using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ClassicalCompactInputTests
{
    [Theory]
    [InlineData(ClassicAverageMethod.Sma)]
    [InlineData(ClassicAverageMethod.Wma)]
    [InlineData(ClassicAverageMethod.Trima)]
    public void FiniteAndWideEntriesPreserveAlignedHistoryAndReset(ClassicAverageMethod method)
    {
        double[] boundaries = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), Math.BitDecrement(Math.ScaleB(1, -1022)),
            1, Math.BitIncrement(1), Math.BitDecrement(1), -1,
            double.MaxValue, -double.MaxValue];
        foreach (int period in new[] { 1, 2, 3, 26, int.MaxValue })
        foreach (int offset in new[] { 0, 7 })
        {
            var owner = new ClassicMovingAverage(period, method);
            var direct = (ClassicMovingAverage.State)owner.CreateAlignedState(owner.First + offset);
            var wide = (ClassicMovingAverage.State)owner.CreateAlignedState(owner.First + offset);
            var actual = new double[2];
            var expected = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                direct.Reset(); wide.Reset();
                for (int i = 0; i < 192; i++)
                {
                    double price = i < 96 ? 100 + i * .125 : boundaries[i % boundaries.Length];
                    var bar = new Bar(default, price, price, price, price, 1);
                    BigInteger units = ExactVarianceWindow.Units(price);
                    if (i % 43 == 42)
                    {
                        units = (BigInteger.One << 2400) * (i % 2 == 0 ? 1 : -1);
                        direct.UpdateUnits(units, actual);
                    }
                    else direct.Update(in bar, actual);
                    wide.UpdateUnits(units, expected);
                    for (int slot = 0; slot < 2; slot++)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot]),
                            BitConverter.DoubleToInt64Bits(actual[slot]));
                    Assert.Equal(wide.RoundedUnits, direct.RoundedUnits);
                }
            }
        }
    }

    [Theory]
    [InlineData(ClassicAverageMethod.Sma)]
    [InlineData(ClassicAverageMethod.Wma)]
    [InlineData(ClassicAverageMethod.Trima)]
    public void InvalidFiniteEntryDoesNotAdvanceState(ClassicAverageMethod method)
    {
        var owner = new ClassicMovingAverage(3, method);
        var state = (ClassicMovingAverage.State)owner.CreateState();
        var reference = (ClassicMovingAverage.State)owner.CreateState();
        var actual = new double[2];
        var expected = new double[2];
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var bad = new Bar(default, invalid, invalid, invalid, invalid, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(in bad, actual));
            for (int i = 0; i < 8; i++)
            {
                var bar = new Bar(default, i, i, i, i, 1);
                state.Update(in bar, actual);
                reference.UpdateUnits(ExactVarianceWindow.Units(i), expected);
                Assert.Equal(expected, actual);
                Assert.Equal(reference.RoundedUnits, state.RoundedUnits);
            }
        }
    }
}
