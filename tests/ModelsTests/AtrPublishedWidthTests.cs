using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AtrPublishedWidthTests
{
    private static void Check(double upper, double lower, double center)
    {
        var numerator = new ExactMeanAccumulator();
        numerator.Add(upper);
        numerator.Add(lower, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(center);
        Assert.Equal(BitConverter.DoubleToInt64Bits(numerator.Ratio(denominator)),
            BitConverter.DoubleToInt64Bits(WindowAtrBands.PublishedWidth(upper, lower, center)));
    }

    [Fact]
    public void FiniteBoundaryTriplesPreserveRoundingAndZeroSigns()
    {
        double[] values = [0d, BitConverter.Int64BitsToDouble(long.MinValue), double.Epsilon,
            -double.Epsilon, Math.ScaleB(1, -1022), 1, Math.BitIncrement(1), -1,
            double.MaxValue, -double.MaxValue];
        foreach (double upper in values)
            foreach (double lower in values)
                foreach (double center in values)
                    if (center != 0) Check(upper, lower, center);
    }

    [Fact]
    public void InexactAndOverflowingSubtractionsRetainExactFallback()
    {
        Check(1, double.Epsilon, 3);
        Check(-1, double.Epsilon, -3);
        Check(double.MaxValue, -double.MaxValue, double.MaxValue);
        Check(double.Epsilon, 1, -double.MaxValue);
    }

    [Fact]
    public void PriceLikeAndWideTriplesMatchExactPublication()
    {
        var random = new Random(20261105);
        for (int i = 0; i < 4000; i++)
        {
            double center = Math.ScaleB(random.NextDouble() + 1, random.Next(-1000, 1000));
            double half = center * random.NextDouble() / 4;
            Check(center + half, center - half, center);
            double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
            Check(Next(), Next(), center);
        }
    }
}
