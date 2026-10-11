using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CertifiedLogReturnTests
{
    [Fact]
    public void CompactNormalizationAndAdaptiveIntervalsMatchIndependentRationalLog()
    {
        double[] values = [double.Epsilon, 3 * double.Epsilon, Math.ScaleB(1, -1022),
            Math.BitDecrement(1), 1, Math.BitIncrement(1), Math.BitDecrement(2), 2,
            Math.BitIncrement(2), .1, 1.23456789012345, double.MaxValue];
        foreach (double current in values)
        foreach (double previous in values)
            Check(current, previous);
        var random = new Random(91207);
        for (int i = 0; i < 128; i++)
        {
            double current = Math.ScaleB(1 + random.NextDouble(), random.Next(-1022, 1023));
            double previous = i % 3 == 0 ? Math.BitIncrement(current)
                : Math.ScaleB(1 + random.NextDouble(), random.Next(-1022, 1023));
            Check(current, previous);
        }
    }
    private static void Check(double current, double previous)
    {
        double expected = (ReferenceFraction.FromDouble(current) / ReferenceFraction.FromDouble(previous)).LogToDouble();
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(CertifiedLogReturn.Of(current, previous)));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(CertifiedLogReturn.Of(-current, -previous)));
    }
}
