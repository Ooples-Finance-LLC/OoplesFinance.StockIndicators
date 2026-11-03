using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class BetaSquaredDifferenceTests
{
    public static TheoryData<double, double> Examples => new()
    {
        { 0, -0d }, { 1, 1 }, { 2, 1 }, { Math.BitIncrement(1), 1 },
        { Math.ScaleB(1, -537), 0 }, { Math.ScaleB(1, -538), 0 },
        { double.MaxValue, -double.MaxValue }, { 1e200, -1e200 },
        { 1e-200, -1e-200 }, { 1e300, 1e-300 }, { -1.5, 2.5 }
    };

    private static void Check(double up, double down)
    {
        var difference = ExactVarianceWindow.Units(up) - ExactVarianceWindow.Units(down);
        double expected = ExactMeanAccumulator.UnitRatio(difference * difference, BigInteger.One << 1074);
        double actual = WindowBetaStatistics.SquaredDifference(up, down);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void NormalSubnormalWideAndOverflowCasesMatchOriginalPublication(double up, double down) => Check(up, down);

    [Fact]
    public void RandomRoundedInputsRetainSingleFinalRounding()
    {
        var random = new Random(12071);
        for (int i = 0; i < 3000; i++)
        {
            double Next() => i < 1000 ? random.NextDouble() * 16 - 8
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
            Check(Next(), Next());
        }
    }
}
