using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MobilityDensityNumericalTests
{
    private static ReferenceFraction Exhaustive((BigInteger Low, BigInteger High)[] candles, BigInteger price, int bins)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        if (candles.Length == 0) return zero;
        var minimum = new ReferenceFraction(candles.Min(c => c.Low));
        var maximum = new ReferenceFraction(candles.Max(c => c.High));
        if (minimum.CompareTo(maximum) == 0) return zero;
        var width = (maximum - minimum) / new ReferenceFraction(bins);
        var masses = new ReferenceFraction[bins]; var priceMass = zero; var mode = 0;
        for (var k = 0; k < bins; k++)
        {
            var lower = minimum + width * new ReferenceFraction(k); var upper = lower + width;
            var mass = zero;
            foreach (var candle in candles)
            {
                var low = new ReferenceFraction(candle.Low); var high = new ReferenceFraction(candle.High);
                if (low.CompareTo(high) == 0)
                {
                    if (low.CompareTo(lower) >= 0 && (low.CompareTo(upper) < 0 || k == bins - 1)) mass += one;
                }
                else
                {
                    ReferenceFraction Cdf(ReferenceFraction edge)
                        => edge.CompareTo(low) <= 0 ? zero : edge.CompareTo(high) >= 0 ? one : (edge - low) / (high - low);
                    mass += Cdf(upper) - Cdf(lower);
                }
            }
            masses[k] = mass;
            if (mass.CompareTo(masses[mode]) > 0) mode = k;
            var comparison = new ReferenceFraction(price);
            if (comparison.CompareTo(lower) >= 0 && (comparison.CompareTo(upper) < 0 || k == bins - 1 && comparison.CompareTo(upper) == 0)) priceMass = mass;
        }
        if (masses[mode].Sign == 0) return zero;
        var center = minimum + (new ReferenceFraction(mode) + one / new ReferenceFraction(2)) * width;
        var direction = new ReferenceFraction(price).CompareTo(center) < 0 ? 100 : -100;
        return new ReferenceFraction(direction) * (one - priceMass / masses[mode]);
    }

    [Fact]
    public void SparseCandidatesMatchIndependentExhaustiveCdf()
    {
        var random = new Random(666);
        for (var trial = 0; trial < 240; trial++)
        {
            var candles = Enumerable.Range(0, random.Next(1, 8)).Select(_ =>
            {
                var low = random.Next(-20, 21); return (Low: new BigInteger(low), High: new BigInteger(low + random.Next(0, 16)));
            }).ToArray();
            var price = new BigInteger(random.Next(-25, 41)); var bins = random.Next(1, 21);
            var expected = Exhaustive(candles, price, bins).ToDouble();
            Assert.Equal(expected, MobilityDensity.Evaluate(candles, price, bins).Publish());
            var scale = BigInteger.One << 2100;
            var translated = candles.Select(c => (Low: (c.Low + 200) * scale, High: (c.High + 200) * scale)).ToArray();
            Assert.Equal(expected, MobilityDensity.Evaluate(translated, (price + 200) * scale, bins).Publish());
        }
    }

    [Fact]
    public void ExtremeBinCountsRetainPointMassAndUniformDensity()
    {
        var b = int.MaxValue;
        var points = new[] { (BigInteger.Zero, BigInteger.Zero), (new BigInteger(2), new BigInteger(2)) };
        Assert.Equal(-100, MobilityDensity.Evaluate(points, BigInteger.One, b).Publish());
        Assert.Equal(0, MobilityDensity.Evaluate(points, BigInteger.Zero, b).Publish());
        Assert.Equal(0, MobilityDensity.Evaluate(points, new BigInteger(2), b).Publish());
        var uniform = new[] { (BigInteger.Zero, new BigInteger(2)) };
        Assert.Equal(0, MobilityDensity.Evaluate(uniform, BigInteger.One, b).Publish());
        var mixture = new[] { (BigInteger.Zero, new BigInteger(2)), (new BigInteger(2), new BigInteger(2)) };
        var expected = (new ReferenceFraction(100) * new ReferenceFraction(b) / new ReferenceFraction((long)b + 1)).ToDouble();
        Assert.Equal(expected, MobilityDensity.Evaluate(mixture, BigInteger.Zero, b).Publish());
        Assert.Equal(0, MobilityDensity.Evaluate(mixture, BigInteger.Zero, 1).Publish());
    }

    [Fact]
    public void DistinctNearTiedMassesSelectTheTrueMode()
    {
        var m = BigInteger.One << 40;
        var candles = new[] { (BigInteger.Zero, 4 * m), (BigInteger.Zero, 2 * m), (2 * m, 4 * m - 1) };
        // Bins 0/1 have mass 3/4. Bin 2 exceeds that by 1/(4M-2),
        // so the mode is bin 2 even though the gap is below 1e-12.
        var expected = (new ReferenceFraction(200) / new ReferenceFraction(6 * m - 1)).ToDouble();
        Assert.True(expected > 0);
        Assert.Equal(expected, Exhaustive(candles, m, 4).ToDouble());
        Assert.Equal(expected, MobilityDensity.Evaluate(candles, m, 4).Publish());
    }

    [Fact]
    public void EmptyAndFlatSamplesHaveNoDensityDifference()
    {
        Assert.Equal(0, MobilityDensity.Evaluate(Array.Empty<(BigInteger, BigInteger)>(), BigInteger.One, 10).Publish());
        Assert.Equal(0, MobilityDensity.Evaluate(new[] { (BigInteger.One, BigInteger.One) }, BigInteger.Zero, int.MaxValue).Publish());
    }
}
