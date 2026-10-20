using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateLogCacheTests
{
    // Preserved rational series, independent of the production cache.
    private static UltimatePowerWeights.Bounds SmallLog(Fraction value, int bits)
    {
        var z = (value - 1) / (value + 1); var square = z * z;
        var term = z; Fraction sum = 0; var index = 0; var tolerance = Fraction.Grid(bits);
        while (true)
        {
            sum += 2 * term / (2 * index + 1); term *= square; index++;
            var tail = 2 * term / ((2 * index + 1) * (1 - square));
            if (tail <= tolerance) return new(sum, sum + tail);
        }
    }

    private static void Same(UltimatePowerWeights.Bounds expected, UltimatePowerWeights.Bounds actual)
    {
        Assert.Equal(expected.Lower.Numerator, actual.Lower.Numerator);
        Assert.Equal(expected.Lower.Denominator, actual.Lower.Denominator);
        Assert.Equal(expected.Upper.Numerator, actual.Upper.Numerator);
        Assert.Equal(expected.Upper.Denominator, actual.Upper.Denominator);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(128)]
    [InlineData(192)]
    [InlineData(256)]
    [InlineData(512)]
    public void RepeatedRatiosKeepExactEndpointsAtEveryPrecision(int bits)
    {
        foreach (var value in new[] { new Fraction(1, 1), new Fraction(17, 16),
            new Fraction(199, 100), new Fraction(int.MaxValue, int.MaxValue - 1),
            new Fraction((BigInteger.One << 80) + 1, BigInteger.One << 80) })
        {
            var expected = SmallLog(value, bits + 2);
            for (var repeat = 0; repeat < 3; repeat++)
                Same(expected, UltimatePowerWeights.Log(value, bits));
        }
    }

    [Fact]
    public void EvictionDoesNotChangeResults()
    {
        var value = new Fraction(17, 16);
        var expected = SmallLog(value, 98);
        Same(expected, UltimatePowerWeights.Log(value, 96));
        for (var i = 1; i <= 600; i++)
            _ = UltimatePowerWeights.Log(new Fraction(10000 + i, 10000), 96);
        Same(expected, UltimatePowerWeights.Log(value, 96));
    }

    [Fact]
    public void ParallelThreadsRetainExactEndpoints()
    {
        var expected = SmallLog(new Fraction(19, 16), 98);
        Parallel.For(0, 32, _ => Same(expected, UltimatePowerWeights.Log(new Fraction(19, 16), 96)));
    }
}
