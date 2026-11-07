using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class GatorDirectDifferenceTests
{
    [Fact]
    public void DistancesMatchExactArithmeticIncludingZerosSubnormalsAndOverflow()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022),
            1, -1, Math.BitIncrement(1), Math.BitDecrement(1),
            double.MaxValue, -double.MaxValue,
            Math.BitIncrement(Math.ScaleB(1, -54)), -Math.BitIncrement(Math.ScaleB(1, -54))];
        foreach (double a in values)
        foreach (double b in values)
        {
            var exact = new ExactMeanAccumulator();
            exact.Add(a); exact.Add(b, -1);
            double distance = Math.Abs(exact.Mean(1));
            AlligatorSample[] input = [new(default, a, b, a)];
            if (double.IsInfinity(distance))
            {
                Assert.Throws<ArithmeticException>(() => GatorWithDetails.FromLines(input).ToArray());
                continue;
            }
            var result = Assert.Single(GatorWithDetails.FromLines(input));
            Assert.Equal(BitConverter.DoubleToInt64Bits(distance), BitConverter.DoubleToInt64Bits(result.Upper!.Value));
            Assert.Equal(BitConverter.DoubleToInt64Bits(-distance), BitConverter.DoubleToInt64Bits(result.Lower!.Value));
            Assert.Null(result.UpperIsExpanding);
            Assert.Null(result.LowerIsExpanding);
        }
        var missing = GatorWithDetails.FromLines([
            new(default, 3, 2, 1), new(default, null, 2, null), new(default, 3, 2, 1)]).ToArray();
        Assert.Null(missing[1].Upper);
        Assert.Null(missing[1].Lower);
        Assert.Equal(false, missing[1].UpperIsExpanding);
        Assert.Equal(false, missing[1].LowerIsExpanding);
        Assert.Null(missing[2].UpperIsExpanding);
        Assert.Null(missing[2].LowerIsExpanding);
    }
}
