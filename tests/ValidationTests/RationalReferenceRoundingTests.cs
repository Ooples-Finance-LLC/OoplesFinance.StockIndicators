using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RationalReferenceRoundingTests
{
    public static IEnumerable<object[]> AdjacentValues() => SmaExtremeRangeTests.AdjacentValues();

    [Theory]
    [MemberData(nameof(AdjacentValues))]
    public void ExactMidpointResidualsDetermineTheCorrectNeighbor(long lowerBits, int sign)
    {
        var lower = BitConverter.Int64BitsToDouble(lowerBits);
        var upper = BitConverter.Int64BitsToDouble(lowerBits + 1);
        var midpoint = (ReferenceFraction.FromDouble(lower) + ReferenceFraction.FromDouble(upper)) / new ReferenceFraction(2);
        var perturbation = ReferenceFraction.FromDouble(double.Epsilon) / new ReferenceFraction(4);
        var direction = new ReferenceFraction(sign);
        AssertBits(sign * lower, ((midpoint - perturbation) * direction).ToDouble());
        AssertBits(sign * upper, ((midpoint + perturbation) * direction).ToDouble());
        var even = BitConverter.Int64BitsToDouble((lowerBits & 1) == 0 ? lowerBits : lowerBits + 1);
        AssertBits(sign * even, (midpoint * direction).ToDouble());
    }

    [Fact]
    public void FiniteBoundaryAndOverflowUseTheSameRoundingRule()
    {
        var maximum = ReferenceFraction.FromDouble(double.MaxValue);
        Assert.Equal(double.MaxValue, maximum.ToDouble());
        Assert.Equal(double.PositiveInfinity, (maximum * new ReferenceFraction(2)).ToDouble());
        Assert.Equal(double.NegativeInfinity, (maximum * new ReferenceFraction(-2)).ToDouble());
        var halfUlp = ReferenceFraction.FromDouble(Math.Pow(2, 970));
        var epsilon = ReferenceFraction.FromDouble(double.Epsilon);
        Assert.Equal(double.MaxValue, (maximum + halfUlp - epsilon).ToDouble());
        Assert.Equal(double.PositiveInfinity, (maximum + halfUlp).ToDouble());
    }

    private static void AssertBits(double expected, double actual)
        => Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    [Theory]
    [MemberData(nameof(AdjacentValues))]
    public void SquareRootRoundingUsesExactMidpoints(long lowerBits, int sign)
    {
        var lower = BitConverter.Int64BitsToDouble(lowerBits);
        var upper = BitConverter.Int64BitsToDouble(lowerBits + 1);
        var midpoint = (ReferenceFraction.FromDouble(lower) + ReferenceFraction.FromDouble(upper)) / new ReferenceFraction(2);
        var perturbation = ReferenceFraction.FromDouble(double.Epsilon) / new ReferenceFraction(4);
        var root = midpoint + perturbation * new ReferenceFraction(sign);
        AssertBits(sign < 0 ? lower : upper, (root * root).SqrtToDouble());
        var even = BitConverter.Int64BitsToDouble((lowerBits & 1) == 0 ? lowerBits : lowerBits + 1);
        AssertBits(even, (midpoint * midpoint).SqrtToDouble());
    }

    [Fact]
    public void SquareRootChecksDomainAndOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceFraction(-1).SqrtToDouble());
        Assert.Equal(0, new ReferenceFraction(0).SqrtToDouble());
        var maximum = ReferenceFraction.FromDouble(double.MaxValue);
        Assert.Equal(double.MaxValue, (maximum * maximum).SqrtToDouble());
        var threshold = maximum + ReferenceFraction.FromDouble(Math.Pow(2, 970));
        Assert.Equal(double.PositiveInfinity, (threshold * threshold).SqrtToDouble());
    }
}
