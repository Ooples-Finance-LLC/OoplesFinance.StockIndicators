using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanAdaptiveReferenceEquivalenceTests
{
    [Fact]
    public void UnreducedFinalRoundingMatchesNormalizedRatios()
    {
        foreach (var numerator in new[] { -3, -1, 0, 1, 3 })
        foreach (var denominator in new[] { 1, 2, 3, 7 })
        foreach (var shift in new[] { 0, 1074, 1200 })
        {
            var d = new BigInteger(denominator) << shift;
            var expected = (new ReferenceFraction(numerator) / new ReferenceFraction(d)).ToDouble();
            var common = (BigInteger.One << 2100) + 123;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                BitConverter.DoubleToInt64Bits(ReferenceFraction.RatioToDouble(numerator * common, d * common)));
        }
        Assert.Throws<DivideByZeroException>(() => ReferenceFraction.RatioToDouble(1, 0));
    }

    [Fact]
    public void IntegerCoordinatesMatchOriginalCenteredFractions()
    {
        foreach (var length in new[] { 2, 4 })
        foreach (var exponent in new[] { 0, 1, 3 })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        {
            var bars = new[] { -3d, -5, 1, -1, 0, 2, 2, -4 }
                .Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p * scale, p * scale, p * scale, p * scale, 1)).ToArray();
            var expected = OriginalCenteredValues(bars, length, exponent);
            var actual = BuiltInFormulaReferences.KaufmanAdaptiveIntegerValues(bars, length, exponent);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], actual[key]);
        }
    }

    private static Dictionary<string, double[]> OriginalCenteredValues(IReadOnlyList<Bar> bars, int length, int exponent)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var mean = zero; var variance = zero;
        var middle = new double[bars.Count]; var upper = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += (prices[j] - prices[j - 1]).Abs();
            var efficiency = travel.Sign == 0 ? zero : (prices[i] - prices[i - length]).Abs() / travel;
            var weight = one;
            for (var power = 0; power < exponent; power++) weight *= efficiency;
            // Centered online variance is independent of production's raw second moment.
            var difference = prices[i] - mean;
            variance = (one - weight) * (variance + weight * difference * difference);
            mean += weight * difference;
            middle[i] = mean.ToDouble();
            upper[i] = KaufmanReferenceBand(mean, variance, 1);
            lower[i] = KaufmanReferenceBand(mean, variance, -1);
        }
        return new() { ["UpperBand"] = upper, ["MiddleBand"] = middle, ["LowerBand"] = lower };
    }

    private static double KaufmanReferenceBand(ReferenceFraction center, ReferenceFraction variance, int direction)
    {
        if (variance.Sign == 0) return center.ToDouble();
        var (vn, vd) = variance.Components;
        int Compare(ReferenceFraction value, ReferenceFraction mean, int side)
        {
            var (n, d) = value.Components; var (mn, md) = mean.Components;
            var distance = (n * md - mn * d) * side;
            if (distance.Sign < 0) return side;
            var denominator = d * md;
            return side * (vn * denominator * denominator).CompareTo(distance * distance * vd);
        }
        var negative = Compare(new ReferenceFraction(0), center, direction) < 0;
        if (negative) { center = new ReferenceFraction(0) - center; direction = -direction; }
        long low = 0, high = 0x7fefffffffffffff;
        while (low < high)
        {
            var candidate = low + (high - low + 1) / 2;
            if (Compare(ReferenceFraction.FromDouble(BitConverter.Int64BitsToDouble(candidate)), center, direction) >= 0) low = candidate;
            else high = candidate - 1;
        }
        var a = ReferenceFraction.FromDouble(BitConverter.Int64BitsToDouble(low));
        var b = low == 0x7fefffffffffffff ? new ReferenceFraction(BigInteger.One << 1024)
            : ReferenceFraction.FromDouble(BitConverter.Int64BitsToDouble(low + 1));
        var comparison = Compare((a + b) / new ReferenceFraction(2), center, direction);
        var result = BitConverter.Int64BitsToDouble(comparison < 0 || comparison == 0 && (low & 1) == 0 ? low : low + 1);
        return negative ? -result : result;
    }
}
