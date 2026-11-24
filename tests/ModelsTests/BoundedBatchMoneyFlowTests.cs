using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class BoundedBatchMoneyFlowTests
{
    [Fact]
    public void NormalExponentSweepMatchesIndependentFractions()
    {
        for (int exponent = -1021; exponent <= 1023; exponent++)
        {
            double high = Math.ScaleB(1.5, exponent), low = high * .5;
            foreach (double close in new[] { low, Math.BitIncrement(low), Math.BitDecrement(high), high })
            foreach (double volume in new[] { 8d, -.1 }) Check(high, low, close, volume);
        }
    }

    [Fact]
    public void BoundariesAndUnsupportedPricesMatchIndependentFractions()
    {
        foreach (int exponent in new[] { -1021, -53, 0, 500, 1023 })
        {
            double pivot = Math.ScaleB(1, exponent);
            foreach (double high in new[] { Math.BitDecrement(pivot), pivot, Math.BitIncrement(pivot) })
            foreach (double low in new[] { Math.BitDecrement(high * .5), high * .5, Math.BitIncrement(high * .5) })
            foreach (double close in new[] { Math.BitDecrement(low), low, low + (high - low) * .5, high, Math.BitIncrement(high) })
                Check(high, low, close, 8);
        }
        Check(3 * double.Epsilon, double.Epsilon, 2 * double.Epsilon, .1);
        Check(-1, -2, -1.25, -.1);
        Check(1, -1, .25, .1);
        Check(1, 2, 1.25, -.1);
        Check(2, 2, 3, double.MaxValue);
        Check(double.MaxValue, double.MaxValue * .5, double.MaxValue, 0);
    }

    [Fact]
    public void PreviewResetAndExtendedCancellationPreserveOriginalState()
    {
        var expected = new MoneyFlowAccumulationWindow();
        var actual = new MoneyFlowAccumulationWindow();
        for (int replay = 0; replay < 2; replay++)
        {
            foreach (var (close, volume) in new[] { (2d, double.MaxValue), (2d, double.MaxValue),
                (1d, double.MaxValue), (1d, double.MaxValue), (1.5, double.MaxValue), (1.75, .1) })
            {
                Same(expected.Next(2, 1, close, volume, false), actual.NextBatch(2, 1, close, volume, false));
                Same(expected.Next(2, 1, close, volume, true), actual.NextBatch(2, 1, close, volume, true));
            }
            expected.Reset(); actual.Reset();
        }
        static void Same(RocBankValue left, RocBankValue right)
        {
            Assert.Equal(left.UpperShift, right.UpperShift);
            Equal(left.Mantissa, right.Mantissa);
            Equal(left.Publish(), right.Publish());
        }
    }

    private static void Check(double high, double low, double close, double volume)
    {
        var h = ReferenceFraction.FromDouble(high);
        var l = ReferenceFraction.FromDouble(low);
        double expected = high == low ? 0 :
            ((ReferenceFraction.FromDouble(close) * new ReferenceFraction(2) - h - l)
                * ReferenceFraction.FromDouble(volume) / (h - l)).ToDouble();
        var window = new MoneyFlowAccumulationWindow();
        Equal(expected, window.NextBatch(high, low, close, volume, true).Publish());
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
