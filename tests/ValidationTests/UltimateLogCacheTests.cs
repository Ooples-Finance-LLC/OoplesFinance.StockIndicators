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

    private static UltimatePowerWeights.Bounds ReferenceLog(Fraction value, int bits)
    {
        var power = 0;
        while (value < 1) { value *= 2; power--; }
        while (value >= 2) { value /= 2; power++; }
        var precision = bits + (int)new BigInteger(Math.Abs(power)).GetBitLength() + 2;
        var core = SmallLog(value, precision);
        var two = SmallLog(2, precision);
        return power >= 0
            ? new(core.Lower + power * two.Lower, core.Upper + power * two.Upper)
            : new(core.Lower + power * two.Upper, core.Upper + power * two.Lower);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    public void ScaledRatiosKeepEveryExactEndpoint(int bits)
    {
        foreach (var value in new[] { new Fraction(1, 50), new Fraction(19, 50),
            new Fraction(2, 1), new Fraction(37, 2), new Fraction(int.MaxValue, 1),
            new Fraction(1, int.MaxValue), new Fraction(BigInteger.One << 80, 3) })
        {
            var expected = ReferenceLog(value, bits);
            for (var repeat = 0; repeat < 3; repeat++)
                Same(expected, UltimatePowerWeights.Log(value, bits));
        }
    }

    [Fact]
    public void CompleteLogEvictionPreservesScaledEndpoints()
    {
        var value = new Fraction(19, 50);
        var expected = ReferenceLog(value, 96);
        Same(expected, UltimatePowerWeights.Log(value, 96));
        for (var i = 1; i <= 600; i++)
            _ = UltimatePowerWeights.Log(new Fraction(i, 10000), 96);
        Same(expected, UltimatePowerWeights.Log(value, 96));
    }

    [Fact]
    public void CompleteLogThreadsKeepPrecisionAndScaleSeparate()
    {
        var values = new[] { new Fraction(19, 50), new Fraction(37, 2), new Fraction(1, 50) };
        var expected = values.Select(value => ReferenceLog(value, 96)).ToArray();
        Parallel.For(0, 24, index =>
        {
            var item = index % values.Length;
            Same(expected[item], UltimatePowerWeights.Log(values[item], 96));
        });
    }

    [Fact]
    public void NonpositiveLogArgumentsStillRejectAfterCacheWarmup()
    {
        _ = UltimatePowerWeights.Log(new Fraction(1, 50), 96);
        Assert.Throws<ArgumentOutOfRangeException>(() => UltimatePowerWeights.Log(0, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => UltimatePowerWeights.Log(-1, 96));
    }
}
