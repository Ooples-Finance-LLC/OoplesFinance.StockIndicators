using OoplesFinance.StockIndicators.Helpers;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using T = OoplesFinance.StockIndicators.Helpers.ConfluenceTrigonometry;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ConfluenceTrigonometryTests
{
    private static readonly long[] Factors = ConfluenceRootRelations.PrimeFactors(new long[] { 7, 11, 13 });
    [Fact]
    public void RationalAngleHandsAndRegularPolygonsRemainExact()
    {
        Assert.Equal(0, (T.Wave(30, 1, 0) - T.Constant(new F(1, 2))).Sign(Factors));
        Assert.Equal(0, (T.Wave(60, 0, 1) - T.Constant(new F(1, 2))).Sign(Factors));
        Assert.Equal(0, T.Wave(45, 1, -1).Sign(Factors));
        Assert.Equal(1, (T.Wave(60, 1, 0) - T.Constant(new F(4, 5))).Sign(Factors));
        Assert.Equal(-1, (T.Wave(60, 1, 0) - T.Constant(new F(9, 10))).Sign(Factors));
        foreach (var n in new[] { 5, 7, 11, 13 })
        {
            var expression = T.Constant(1);
            for (var k = 1; k < n; k++) expression += T.Wave(new F(360 * k, n), 0, 1);
            Assert.Equal(0, expression.Sign(Factors));
        }
    }
    [Fact]
    public void SubnormalLinearQuadraticAndCubicSignsSurviveCancellation()
    {
        var tiny = F.Of(double.Epsilon);
        Assert.Equal(1, T.Wave(tiny, 1, 0).Sign(Factors));
        Assert.Equal(-1, (T.Wave(tiny, 0, 1) - T.Constant(1)).Sign(Factors));
        // sin(x) - sin(2*x)/2 = sin(x)*(1-cos(x)) is strictly positive here.
        Assert.Equal(1, (T.Wave(tiny, 1, 0) - T.Wave(2 * tiny, new F(1, 2), 0)).Sign(Factors));
        Assert.Equal(1, (T.Wave(30 + tiny, 1, 0) - T.Wave(30, 1, 0)).Sign(Factors));
        Assert.Equal(-1, (T.Wave(30 + tiny, 0, 1) - T.Wave(30, 0, 1)).Sign(Factors));
    }
    [Fact]
    public void ExactPeriodicityAndSharedZeroRemainImmutable()
    {
        var angle = F.Of(double.MaxValue);
        Assert.Equal(0, (T.Wave(angle, 1, 1) - T.Wave(angle + 360, 1, 1)).Sign(Factors));
        var negative = T.Zero - T.Wave(30, 1, 0);
        Assert.Equal(-1, negative.Sign(Factors)); Assert.Equal(0, T.Zero.Sign(Factors));
        Assert.Equal(0, (T.Wave(190, 1, 0) + T.Wave(10, 1, 0)).Sign(Factors));
    }
}
