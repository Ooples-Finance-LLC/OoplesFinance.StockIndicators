using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PvoAdjacentCertificateTests
{
    [Theory]
    [InlineData(76362.3881782202, 421728.9026506446, 12)]
    [InlineData(50033.09499230819, 307831.209972335, 12)]
    [InlineData(374162.5718871011, 228601.62516423903, 26)]
    [InlineData(85749.83097176904, 434161.4504694833, 26)]
    [InlineData(14307.919183965752, 134878.920825191, int.MaxValue)]
    [InlineData(282502.6544873151, 478092.18029528554, int.MaxValue)]
    public void AdjacentCellsRecoverExactRoundingAcrossSignsAndScales(double previous, double current, int period)
    {
        foreach (int sign in new[] { -1, 1 })
        foreach (int exponent in new[] { -360, 0, 360 })
        {
            double prior = Math.ScaleB(sign * previous, exponent);
            double input = Math.ScaleB(sign * current, exponent);
            double expected = ((ReferenceFraction.FromDouble(prior) * new ReferenceFraction(period - 1)
                + ReferenceFraction.FromDouble(input) * new ReferenceFraction(2))
                / new ReferenceFraction((long)period + 1)).ToDouble();
            double p = prior * (period - 1), q = input * 2;
            double native = (p + q) / ((long)period + 1);
            Assert.NotEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(native));
            Assert.True(EmaDifferenceSignal.VolumeAverage.TryBlend(prior, input, period, out var actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
    }
}
