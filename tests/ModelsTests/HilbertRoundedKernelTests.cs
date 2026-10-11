using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class HilbertRoundedKernelTests
{
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private static BigInteger Units(double value) => ExactVarianceWindow.Units(value);
    private static void EqualBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    private static bool CheckProduct(double a, double b, double c, double d)
    {
        if (!HilbertRoundedKernel.TryProductSum(a, b, c, d, out double actual)) return false;
        double expected = ExactMeanAccumulator.UnitRatio(Units(a) * Units(b) + Units(c) * Units(d), Grid);
        EqualBits(expected == 0 ? 0 : expected, actual);
        return true;
    }

    [Fact]
    public void CertifiedProductsMatchIntegerReferenceAcrossExponentsAndCancellation()
    {
        var random = new Random(4729);
        int accepted = 0, rejected = 0;
        double Next() => Math.ScaleB((random.NextDouble() - .5) * 2, random.Next(-500, 501));
        for (int i = 0; i < 6000; i++)
        {
            double a = Next(), b = Next(), c = Next(), d = Next();
            if (CheckProduct(a, b, c, d)) accepted++; else rejected++;
            if (CheckProduct(a, .2, b, .8)) accepted++; else rejected++;
            CheckProduct(a, b, -a, b);
            CheckProduct(a, b, -Math.BitIncrement(a), b);
        }
        // Independent exponents intentionally stress conservative rejection.
        // Also require useful acceptance on ordinary comparable price scales.
        int ordinaryAccepted = 0;
        for (int i = 0; i < 6000; i++)
        {
            double a = 100 + random.NextDouble() * 10, b = 100 + random.NextDouble() * 10;
            if (CheckProduct(a, .2, b, .8)) { ordinaryAccepted++; accepted++; }
            else rejected++;
            CheckProduct(a, b, -b, a);
        }
        Assert.True(ordinaryAccepted > 1000, $"Only {ordinaryAccepted} ordinary blends certified.");
        Assert.True(accepted > 1000, $"Only {accepted} certified cases.");
        Assert.True(rejected > 1000, $"Only {rejected} fallback cases.");
    }

    [Fact]
    public void ProductCertificateCoversTiesGuardEdgesAndSignedZero()
    {
        double halfUlp = Math.ScaleB(1, -53);
        foreach (double one in new[] { 1d, Math.BitIncrement(1d) })
        foreach (double tail in new[] { Math.BitDecrement(halfUlp), halfUlp, Math.BitIncrement(halfUlp) })
        {
            Assert.True(CheckProduct(one, 1, tail, 1));
            Assert.True(CheckProduct(-one, 1, -tail, 1));
        }
        foreach (int exponent in new[] { -450, 450 })
        {
            double edge = Math.ScaleB(1, exponent);
            Assert.True(CheckProduct(edge, 1, -edge, 1));
        }
        Assert.True(CheckProduct(-0d, 1, 0, -1));
        foreach (double value in new[] { double.Epsilon, Math.BitDecrement(Math.ScaleB(1, -450)),
            Math.BitIncrement(Math.ScaleB(1, 450)), double.MaxValue, double.PositiveInfinity, double.NaN })
            Assert.False(HilbertRoundedKernel.TryProductSum(value, 1, 0, 1, out _));
        Assert.False(HilbertRoundedKernel.TryProductSum(double.Epsilon, .5, 0, 1, out _));
        Assert.False(HilbertRoundedKernel.TryProductSum(double.MaxValue, 2, 0, 1, out _));
        Assert.False(HilbertRoundedKernel.TryProductSum(0, double.PositiveInfinity, 0, 1, out _));
    }

    [Fact]
    public void ExactSumAndSmoothingCertificatesMatchIntegerReference()
    {
        var random = new Random(7301);
        int accepted = 0;
        for (int i = 0; i < 3000; i++)
        {
            int exponent = random.Next(-1074, 420);
            double a = Math.ScaleB(random.Next(-1024, 1024), exponent);
            double b = Math.ScaleB(random.Next(-1024, 1024), exponent);
            double c = Math.ScaleB(random.Next(-1024, 1024), exponent);
            double d = Math.ScaleB(random.Next(-1024, 1024), exponent);
            Assert.True(HilbertRoundedKernel.TryExactSum(a, b, out double sum));
            Assert.Equal(Units(a) + Units(b), Units(sum));
            if (HilbertRoundedKernel.TrySmooth(a, b, c, d, out double smooth))
            {
                double expected = ExactMeanAccumulator.UnitRatio(4 * Units(a) + 3 * Units(b) + 2 * Units(c) + Units(d), 10);
                EqualBits(expected == 0 ? 0 : expected, smooth);
                accepted++;
            }
        }
        Assert.True(accepted > 2900);
        Assert.False(HilbertRoundedKernel.TryExactSum(1, double.Epsilon, out _));
        Assert.False(HilbertRoundedKernel.TryExactSum(double.MaxValue, double.MaxValue, out _));
        Assert.False(HilbertRoundedKernel.TrySmooth(double.MaxValue, 1, 1, 1, out _));
        Assert.False(HilbertRoundedKernel.TrySmooth(0, Math.BitIncrement(1d), 0, 0, out _));
    }
}
