using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonFloatingPolynomialTests
{
    [Fact]
    public void CertifiedPolynomialMatchesIndependentExpandedRationalCoefficients()
    {
        var random = new Random(281030);
        double[] factors = [-2, -.7, .25, .7, 1, 2];
        int[] exponents = [673, 1029, 1372];
        int accepted = 0;
        for (int sample = 0; sample < 1536; sample++)
        {
            ulong origin = ((ulong)exponents[sample % exponents.Length] << 52)
                | (ulong)random.NextInt64(0, 1L << 50);
            double[] stages = Enumerable.Range(0, 4).Select(_ =>
                BitConverter.Int64BitsToDouble((long)(origin + (ulong)random.NextInt64(0, 1L << 49)))).ToArray();
            double factor = factors[sample % factors.Length];
            if (!CertifiedTillson.TryCombineFloating(stages[0], stages[1], stages[2], stages[3], factor, out double actual)) continue;
            accepted++;
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(stages, factor)), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 1000, $"Only {accepted} bounded polynomial inputs were certified.");
    }

    [Fact]
    public void MidpointsRejectTheFloatingCertificateAndRetainOriginalPublication()
    {
        foreach (int offset in new[] { 5, 7 })
        {
            double[] stages = [Math.BitIncrement(1.125), 1, 1,
                BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1d) + offset)];
            Assert.False(CertifiedTillson.TryCombineFloating(stages[0], stages[1], stages[2], stages[3], 1, out _));
            Assert.True(CertifiedTillson.TryCombine(stages[0], stages[1], stages[2], stages[3], 1, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(stages, 1)), BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void ExactFlatAndSpreadBoundariesCertifyWhileUnsupportedDomainsFallBack()
    {
        foreach (double value in new[] { Math.ScaleB(1d, -350), 1d, 100d, Math.ScaleB(1d, 349) })
            foreach (double factor in new[] { -2d, -.25, .25, .7, 2 })
            {
                Assert.True(CertifiedTillson.TryCombineFloating(value, value, value, value, factor, out double actual));
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(actual));
            }
        Assert.True(CertifiedTillson.TryCombineFloating(1, 1.25, 1, 1.25, .5, out double boundary));
        Assert.Equal(.125, boundary);
        Assert.False(CertifiedTillson.TryCombineFloating(1, Math.BitIncrement(1.25), 1, 1.25, .5, out _));
        Assert.False(CertifiedTillson.TryCombineFloating(1, 2, 1, 1, .7, out _));
        foreach (double value in new[] { 0d, -1d, double.Epsilon, Math.ScaleB(1d, -351), Math.ScaleB(1d, 350), double.MaxValue, double.NaN })
            Assert.False(CertifiedTillson.TryCombineFloating(value, value, value, value, .7, out _));
        foreach (double factor in new[] { 0d, double.Epsilon, 3d, double.NaN, double.PositiveInfinity })
            Assert.False(CertifiedTillson.TryCombineFloating(100, 100, 100, 100, factor, out _));
    }

    private static double Reference(double[] stages, double factor)
    {
        var a = ReferenceFraction.FromDouble(factor);
        var square = a * a; var cube = square * a;
        var one = new ReferenceFraction(1); var three = new ReferenceFraction(3); var minusThree = new ReferenceFraction(-3); var six = new ReferenceFraction(6);
        var result = ReferenceFraction.FromDouble(stages[0]) * (one + three * a + three * square + cube)
            + ReferenceFraction.FromDouble(stages[1]) * (minusThree * a - six * square - three * cube)
            + ReferenceFraction.FromDouble(stages[2]) * (three * square + three * cube)
            - ReferenceFraction.FromDouble(stages[3]) * cube;
        return result.ToDouble();
    }
}
