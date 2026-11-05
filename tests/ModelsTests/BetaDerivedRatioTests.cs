using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class BetaDerivedRatioTests
{
    private static void Check(double up, double down)
    {
        var numerator = new ExactMeanAccumulator();
        numerator.Add(up);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(down);
        Assert.Equal(BitConverter.DoubleToInt64Bits(numerator.Ratio(denominator)),
            BitConverter.DoubleToInt64Bits(WindowBetaStatistics.DerivedRatio(up, down)));
    }

    [Fact]
    public void BoundaryInputsPreserveZeroSignsUnderflowAndOverflow()
    {
        double[] values = [0d, BitConverter.Int64BitsToDouble(long.MinValue),
            double.Epsilon, -double.Epsilon, Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022),
            Math.BitDecrement(1), 1, Math.BitIncrement(1), -1, double.MaxValue, -double.MaxValue];
        foreach (double up in values)
            foreach (double down in values)
                if (down != 0) Check(up, down);
    }

    [Fact]
    public void RoundedFiniteInputsMatchExactRationalPublication()
    {
        var random = new Random(20261105);
        for (int i = 0; i < 4000; i++)
        {
            double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
            double up = Next(), down = Next();
            if (down != 0) Check(up, down);
        }
    }
}
