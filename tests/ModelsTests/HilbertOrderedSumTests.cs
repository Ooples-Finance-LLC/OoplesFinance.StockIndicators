using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class HilbertOrderedSumTests
{
    private static void Check(double a, double b)
    {
        bool accepted = HilbertRoundedKernel.TryExactPhaseSum(a, b, out double sum);
        bool bounded = Math.Abs(a) <= Math.ScaleB(1, 450) && Math.Abs(b) <= Math.ScaleB(1, 450);
        bool exact = bounded && ExactVarianceWindow.Units(a) + ExactVarianceWindow.Units(b)
            == ExactVarianceWindow.Units(sum);
        Assert.Equal(exact, accepted);
        Assert.Equal(BitConverter.DoubleToInt64Bits(a + b), BitConverter.DoubleToInt64Bits(sum));
    }

    [Fact]
    public void OrderedCertificatesMatchIntegerSumsAcrossIndependentExponents()
    {
        var random = new Random(81463);
        for (int i = 0; i < 12000; i++)
        {
            double a = Math.ScaleB(random.Next(-65536, 65537), random.Next(-1074, 451));
            double b = Math.ScaleB(random.Next(-65536, 65537), random.Next(-1074, 451));
            Check(a, b); Check(b, a); Check(a, -a);
        }
    }

    [Fact]
    public void CancellationMidpointsZerosAndSubnormalBoundariesRetainExactDecisions()
    {
        foreach (int exponent in new[] { -1074, -1022, -500, -53, 0, 400, 450 })
        foreach (int sign in new[] { -1, 1 })
        {
            double a = sign * Math.ScaleB(1, exponent);
            foreach (double b in new[] { 0d, -0d, a, -a, Math.BitIncrement(a), Math.BitDecrement(a),
                         double.Epsilon, -double.Epsilon, Math.ScaleB(a, -53) })
            {
                Check(a, b); Check(b, a);
            }
        }
        foreach (double a in new[] { 0d, -0d })
        foreach (double b in new[] { 0d, -0d }) Check(a, b);
        Assert.False(HilbertRoundedKernel.TryExactPhaseSum(1, Math.ScaleB(1, -53), out _));
    }

    [Fact]
    public void NonfiniteAndOutOfBoundsInputsRetainFallback()
    {
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                     double.MaxValue, -double.MaxValue, Math.BitIncrement(Math.ScaleB(1, 450)) })
        {
            Assert.False(HilbertRoundedKernel.TryExactPhaseSum(value, 0, out _));
            Assert.False(HilbertRoundedKernel.TryExactPhaseSum(0, value, out _));
        }
    }
}
