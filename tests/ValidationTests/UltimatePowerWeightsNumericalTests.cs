using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimatePowerWeightsNumericalTests
{
    private static void Contains(UltimatePowerWeights.Bounds bounds, Fraction value, int bits = 96)
    {
        Assert.True(bounds.Lower <= value && bounds.Upper >= value);
        Assert.True(bounds.Upper - bounds.Lower <= Fraction.Grid(bits));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public void IntegerPowerSumsMatchIndependentClosedForms(int length)
    {
        BigInteger n = length;
        var sums = new[]
        {
            new Fraction(n, 1), new Fraction(n * (n + 1), 2), new Fraction(n * (n + 1) * (2 * n + 1), 6),
            new Fraction(n * n * (n + 1) * (n + 1), 4),
            new Fraction(n * (n + 1) * (2 * n + 1) * (3 * n * n + 3 * n - 1), 30),
            new Fraction(n * n * (n + 1) * (n + 1) * (2 * n * n + 2 * n - 1), 12)
        };
        for (var p = 0; p < sums.Length; p++)
        {
            var expected = sums[p] / new Fraction(BigInteger.Pow(n, p), 1);
            var actual = UltimatePowerWeights.Sum(length, p, 96);
            Contains(actual, expected); Assert.Equal(0, (actual.Upper - actual.Lower).Sign);
        }
    }

    [Fact]
    public void FractionalPowersEncloseRationalSquareRoots()
    {
        foreach (var pair in new[] { (1, 2), (2, 3), (31, 32), (2147483646, int.MaxValue) })
        {
            var root = new Fraction(pair.Item1, pair.Item2);
            Contains(UltimatePowerWeights.Power(root * root, new Fraction(1, 2), 128), root);
            Contains(UltimatePowerWeights.Power(1 / (root * root), new Fraction(-1, 2), 128), root);
        }
    }

    [Fact]
    public void FractionalSumEnclosesIndependentIntegerSquareRootBounds()
    {
        // sqrt(k/n) is enclosed by sqrt(k*n*2^(2b))/(n*2^b).
        // The reference uses only integer multiplication and binary-search roots.
        const int n = 17, precision = 160;
        Fraction lower = 0, upper = 0;
        for (var k = 1; k <= n; k++)
        {
            var square = (BigInteger)k * n << (2 * precision);
            BigInteger left = 0, right = (BigInteger)n << precision;
            while (right - left > 1)
            {
                var mid = (left + right) / 2;
                if (mid * mid <= square) left = mid; else right = mid;
            }
            var denominator = (BigInteger)n << precision;
            lower += new Fraction(left, denominator); upper += new Fraction(right, denominator);
        }
        var actual = UltimatePowerWeights.Sum(n, new Fraction(1, 2), 96);
        Assert.True(actual.Lower <= upper && actual.Upper >= lower);
        Assert.True(actual.Upper - actual.Lower <= Fraction.Grid(96));
    }

    [Fact]
    public void LogReciprocalAndExtremeNormalizedTailsAreBounded()
    {
        var tiny = Fraction.Of(double.Epsilon); var reciprocal = 1 / tiny;
        var low = UltimatePowerWeights.Log(tiny, 128); var high = UltimatePowerWeights.Log(reciprocal, 128);
        Assert.True(low.Lower + high.Lower <= 0 && low.Upper + high.Upper >= 0);
        foreach (var exponent in new[] { Fraction.Of(double.MaxValue), -Fraction.Of(double.MaxValue) })
        {
            var sum = UltimatePowerWeights.Sum(int.MaxValue, exponent, 128);
            Assert.True(sum.Lower <= 1 && sum.Upper >= 1);
            Assert.True(sum.Upper - sum.Lower <= Fraction.Grid(128));
        }
    }

    [Fact]
    public void FractionArithmeticPreservesSignedSubnormalValuesAndOutwardRounding()
    {
        var tiny = Fraction.Of(double.Epsilon); Assert.Equal(double.Epsilon, tiny.Publish());
        Assert.Equal(-double.Epsilon, (-tiny).Publish());
        Assert.Equal(new BigInteger(-1), new Fraction(-1, 3).Floor());
        Assert.Equal(BigInteger.Zero, new Fraction(-1, 3).Ceiling());
        var bounds = new UltimatePowerWeights.Bounds(new Fraction(-1, 3), new Fraction(2, 3)).Round(96);
        Assert.True(bounds.Lower <= new Fraction(-1, 3) && bounds.Upper >= new Fraction(2, 3));
        var max = Fraction.Of(double.MaxValue); Assert.Equal(double.MaxValue, (max * max / max).Publish());
    }

    [Fact]
    public void WeightedMeanKeepsMissingHistoryInTheDenominator()
    {
        Contains(UltimatePowerWeights.Mean(new Fraction[] { 14 }, 3, 2, 96), 9);
        Contains(UltimatePowerWeights.Mean(new Fraction[] { 14, -7 }, 3, 2, 96), 7);
        Contains(UltimatePowerWeights.Mean(new Fraction[] { 14, -7, 28 }, 3, 2, 96), 9);
        Contains(UltimatePowerWeights.Mean(new Fraction[] { 14, -7, 28, 1000 }, 3, 2, 96), 9);
        Contains(UltimatePowerWeights.Mean(Array.Empty<Fraction>(), int.MaxValue, new Fraction(1, 2), 96), 0);
    }

    [Fact]
    public void WeightedMeanPreservesExactCancellationAndTinyRemainders()
    {
        var big = Fraction.Of(double.MaxValue); var tiny = Fraction.Of(double.Epsilon);
        var values = new[] { big, -big, tiny };
        Contains(UltimatePowerWeights.Mean(values, 3, 0, 96), tiny / 3);
        Contains(UltimatePowerWeights.Mean(values, int.MaxValue, 0, 96), tiny / int.MaxValue);
        // Squared weights are 9:4:1; the first two contributions cancel exactly.
        Contains(UltimatePowerWeights.Mean(new[] { big / 9, -big / 4, tiny }, 3, 2, 96), tiny / 14);
    }

    [Fact]
    public void FractionalMeanRecognizesExactRadicalCancellationAndConstants()
    {
        // sqrt(4/5) - 2*sqrt(1/5) = 0, though neither weight is rational.
        var cancelled = UltimatePowerWeights.Mean(new Fraction[] { 0, 1, 0, 0, -2 }, 5, new Fraction(1, 2), 96);
        Assert.Equal(0, cancelled.Lower.Sign); Assert.Equal(0, cancelled.Upper.Sign);
        var constant = Fraction.Of(double.Epsilon);
        Contains(UltimatePowerWeights.Mean(Enumerable.Repeat(constant, 17).ToArray(), 17, new Fraction(-7, 2), 96), constant);
    }

    [Fact]
    public void IrrationalSubnormalRootRetainsARelativeEnclosure()
    {
        var tiny = Fraction.Of(double.Epsilon); var square = tiny * tiny * 2;
        var bounds = UltimatePowerWeights.Root(square, 96);
        Assert.True(bounds.Lower.Sign > 0);
        Assert.True(bounds.Lower * bounds.Lower <= square && bounds.Upper * bounds.Upper >= square);
        Assert.True(bounds.Upper - bounds.Lower <= tiny * Fraction.Grid(95));
    }
}
