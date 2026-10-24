using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CertifiedHilbertProjectionTests
{
    [Fact]
    public void FloatingProjectionCertificatesMatchIndependentIntegerProducts()
    {
        var random = new Random(904215);
        int accepted = 0;
        foreach (int exponent in new[] { -400, -100, 0, 100, 400 })
        for (int count = 1; count <= 50; count++)
        for (int replay = 0; replay < 4; replay++)
        {
            var prices = Enumerable.Range(0, 50).Select(_ =>
                Math.ScaleB((1 + random.NextDouble()) * (random.Next(2) == 0 ? -1 : 1), exponent)).ToArray();
            if (Check(prices, (count + 13 * replay) % 50, count, floating: true)) accepted++;
        }
        Assert.True(accepted > 900, $"Only {accepted} floating projections certified");
    }

    [Fact]
    public void FloatingProjectionPreservesAxesZerosAndBoundedFallback()
    {
        foreach (int count in new[] { 1, 2, 4, 25, 49, 50 })
        foreach (int cursor in new[] { 0, 17, 49 })
        {
            Check(Enumerable.Repeat(1d, 50).ToArray(), cursor, count, floating: true);
            Check(Enumerable.Range(0, 50).Select(i => i % 2 == 0 ? 1d : -1d).ToArray(), cursor, count, floating: true);
            Check(Enumerable.Range(0, 50).Select(i => i == cursor ? 1d : 0d).ToArray(), cursor, count, floating: true);
        }
        Assert.True(CertifiedHilbertProjection.TryProjectFloating(new JurikDyadic[50], 0, 50, out var real, out var imaginary));
        Equal(0, real.Mean(1)); Equal(0, imaginary.Mean(1));
        foreach (double value in new[] { double.Epsilon, 1e-200, 1e200, double.MaxValue })
            Assert.False(CertifiedHilbertProjection.TryProjectFloating(
                Enumerable.Repeat(JurikDyadic.FromDouble(value), 50).ToArray(), 0, 50, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProjectFloating(new JurikDyadic[49], 0, 1, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProjectFloating(new JurikDyadic[50], -1, 1, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProjectFloating(new JurikDyadic[50], 0, 51, out _, out _));
    }

    [Fact]
    public void CertifiedProjectionsMatchIndependentIntegerProducts()
    {
        var random = new Random(731249);
        int accepted = 0, ordinary = 0;
        foreach (int exponent in new[] { -1000, -500, 0, 500, 1000 })
        foreach (int sign in new[] { -1, 1 })
        for (int count = 1; count <= 50; count++)
        for (int replay = 0; replay < 4; replay++)
        {
            var prices = Enumerable.Range(0, 50)
                .Select(_ => sign * Math.ScaleB(1 + random.NextDouble(), exponent)).ToArray();
            if (Check(prices, (count + replay * 13) % 50, count))
            {
                accepted++;
                if (exponent == 0) ordinary++;
            }
        }
        // Count two can conservatively fall back on its near-axis sine term.
        Assert.True(accepted > 1500, $"Only {accepted} certified projections.");
        Assert.True(ordinary > 300, $"Only {ordinary} ordinary projections certified.");
    }

    [Fact]
    public void CancellationAxisTermsAndSubnormalBoundariesRetainExactFallback()
    {
        foreach (double magnitude in new[] { double.Epsilon, Math.ScaleB(1, -1022), 1d, double.MaxValue })
        foreach (int count in new[] { 1, 2, 4, 25, 49, 50 })
        foreach (int cursor in new[] { 0, 17, 49 })
        {
            Check(Enumerable.Repeat(magnitude, 50).ToArray(), cursor, count);
            Check(Enumerable.Range(0, 50).Select(i => i % 2 == 0 ? magnitude : -magnitude).ToArray(), cursor, count);
            Check(Enumerable.Range(0, 50).Select(i => i == cursor ? magnitude : 0d).ToArray(), cursor, count);
        }
        var values = Enumerable.Repeat(JurikDyadic.FromDouble(1), 50).ToArray();
        values[0] = JurikDyadic.FromDouble(double.Epsilon);
        Assert.False(CertifiedHilbertProjection.TryProject(values, 0, 50, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProject(values, 1, 50, out _, out _));
        values[0] = JurikDyadic.FromDouble(1);
        values[0].Add(double.Epsilon);
        Assert.False(CertifiedHilbertProjection.TryProject(values, 0, 50, out _, out _));
    }

    [Fact]
    public void EmptyProjectionAndInvalidRingHaveExplicitOutcomes()
    {
        Assert.True(CertifiedHilbertProjection.TryProject(new JurikDyadic[50], 0, 0, out var real, out var imaginary));
        Assert.Equal(0, real.Sign);
        Assert.Equal(0, imaginary.Sign);
        Assert.False(CertifiedHilbertProjection.TryProject(new JurikDyadic[49], 0, 1, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProject(new JurikDyadic[50], -1, 1, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProject(new JurikDyadic[50], 50, 1, out _, out _));
        Assert.False(CertifiedHilbertProjection.TryProject(new JurikDyadic[50], 0, 51, out _, out _));
    }

    private static bool Check(double[] prices, int cursor, int count, bool floating = false)
    {
        var values = prices.Select(JurikDyadic.FromDouble).ToArray();
        JurikDyadic real, imaginary;
        bool accepted = floating
            ? CertifiedHilbertProjection.TryProjectFloating(values, cursor, count, out real, out imaginary)
            : CertifiedHilbertProjection.TryProject(values, cursor, count, out real, out imaginary);
        if (!accepted) return false;
        BigInteger sineSum = 0, cosineSum = 0;
        for (int lag = 0; lag < count; lag++)
        {
            double angle = lag * 2d * Math.PI / count;
            var units = ExactVarianceWindow.Units(prices[(cursor - lag + 50) % 50]);
            sineSum += units * ExactVarianceWindow.Units(Math.Sin(angle));
            cosineSum += units * ExactVarianceWindow.Units(Math.Cos(angle));
        }
        Equal(ExactMeanAccumulator.UnitRatio(sineSum, BigInteger.One << 1074), real.Mean(1));
        Equal(ExactMeanAccumulator.UnitRatio(cosineSum, BigInteger.One << 1074), imaginary.Mean(1));
        return true;
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected == 0 ? 0 : expected), BitConverter.DoubleToInt64Bits(actual));
}
