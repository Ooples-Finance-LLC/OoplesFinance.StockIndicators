using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ExactTypicalPriceTests
{
    [Fact]
    public void CertifiedAndFallbackMeansMatchExactAccumulatorBits()
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
            double.MaxValue, -double.MaxValue, 1, -1, Math.BitIncrement(1d), Math.BitDecrement(1d),
            Math.ScaleB(1, -1022), Math.ScaleB(1, 900), Math.ScaleB(1, -900)];
        foreach (double a in values)
            foreach (double b in values)
                foreach (double c in values) Check(a, b, c);
        var random = new Random(714);
        for (int i = 0; i < 4000; i++)
        {
            double a = Math.ScaleB(random.NextDouble() - .5, random.Next(-1074, 1024));
            double b = Math.ScaleB(random.NextDouble() - .5, random.Next(-1074, 1024));
            double c = Math.ScaleB(random.NextDouble() - .5, random.Next(-1074, 1024));
            Check(a, b, c);
            Check(a, b, -a);
        }
    }

    private static void Check(double a, double b, double c)
    {
        var exact = new ExactMeanAccumulator();
        exact.Add(a); exact.Add(b); exact.Add(c);
        Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Mean(3)),
            BitConverter.DoubleToInt64Bits(ExactTypicalPrice.Calculate(a, b, c)));
    }
}
