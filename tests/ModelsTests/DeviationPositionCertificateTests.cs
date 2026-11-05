using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DeviationPositionCertificateTests
{
    private static double Expected(double close, double center, double half)
    {
        var numerator = new ExactMeanAccumulator();
        numerator.Add(close);
        numerator.Add(center, -1);
        numerator.Add(half);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(half, 2);
        return numerator.Ratio(denominator);
    }

    private static void Check(double close, double center, double half) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(Expected(close, center, half)),
            BitConverter.DoubleToInt64Bits(WindowDeviationBands.FinitePosition(close, center, half)));

    [Fact]
    public void FiniteBoundaryTriplesPreserveCanonicalZeroUnderflowAndOverflow()
    {
        double[] values = [0d, BitConverter.Int64BitsToDouble(long.MinValue), double.Epsilon,
            -double.Epsilon, Math.ScaleB(1, -1022), 1, Math.BitIncrement(1), -1,
            double.MaxValue, -double.MaxValue];
        foreach (double close in values)
            foreach (double center in values)
                foreach (double half in values)
                    if (half != 0) Check(close, center, half);
    }

    [Fact]
    public void RoundingEitherIntermediateBeforeCancellationChangesPublication()
    {
        (double Close, double Center, double Half)[] cases =
            [(1, Math.ScaleB(1, -54), -.5), (1, 0, Math.ScaleB(3, -56))];
        foreach (var (close, center, half) in cases)
        {
            double rounded = (close - center + half) / (half * 2);
            Assert.NotEqual(BitConverter.DoubleToInt64Bits(Expected(close, center, half)),
                BitConverter.DoubleToInt64Bits(rounded));
            Check(close, center, half);
        }
    }

    [Fact]
    public void PriceLikeAndWideTriplesMatchExactPublication()
    {
        var random = new Random(20261105);
        for (int i = 0; i < 4000; i++)
        {
            double center = Math.ScaleB(random.NextDouble() + 1, random.Next(-1000, 1000));
            double half = center * (random.NextDouble() + .01) / 4;
            double close = center + half * (random.NextDouble() * 4 - 2);
            Check(close, center, half);
            double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
            close = Next(); center = Next(); half = Next();
            if (half != 0) Check(close, center, half);
        }
    }
}
