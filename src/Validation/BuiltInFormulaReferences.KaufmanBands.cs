using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static BigInteger KaufmanReferenceUnits(double value)
    {
        var fraction = ReferenceFraction.FromDouble(value).Components;
        return fraction.Numerator * ((BigInteger.One << 1074) / fraction.Denominator);
    }
    internal static Dictionary<string, double[]> KaufmanAdaptiveIntegerValues(IReadOnlyList<Bar> bars, int length, int exponent)
    {
        length = Math.Max(1, length);
        var prices = bars.Select(b => KaufmanReferenceUnits(b.Close)).ToArray();
        BigInteger mean = 0, variance = 0, denominator = 1;
        var middle = new double[bars.Count]; var upper = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = BigInteger.Zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += BigInteger.Abs(prices[j] - prices[j - 1]);
            var efficiency = travel.IsZero ? new ReferenceFraction(0)
                : new ReferenceFraction(BigInteger.Abs(prices[i] - prices[i - length])) / new ReferenceFraction(travel);
            var weight = new ReferenceFraction(1);
            for (var power = 0; power < exponent; power++) weight *= efficiency;
            var (gn, gd) = weight.Components;
            if (gn == gd) { mean = prices[i]; variance = 0; denominator = 1; }
            else if (!gn.IsZero)
            {
                // Exact centered recurrence in a denominator chain: mean has
                // denominator D and variance has D^2, in binary64 input units.
                var delta = prices[i] * denominator - mean;
                variance = (gd - gn) * (gd * variance + gn * delta * delta);
                mean = gd * mean + gn * delta;
                denominator *= gd;
            }
            middle[i] = ReferenceFraction.RatioToDouble(mean, denominator << 1074);
            upper[i] = variance.IsZero ? middle[i] : KaufmanReferenceBand(mean, variance, denominator, 1);
            lower[i] = variance.IsZero ? middle[i] : KaufmanReferenceBand(mean, variance, denominator, -1);
        }
        return new() { ["UpperBand"] = upper, ["MiddleBand"] = middle, ["LowerBand"] = lower };
    }

    private static double KaufmanReferenceBand(BigInteger center, BigInteger variance, BigInteger denominator, int direction)
    {
        // Bound the root from its leading 256 bits. Usually both rational endpoints
        // round identically, avoiding a binary search with 63 very large squares.
        // Ambiguous cases retain the independent exact-square comparison below.
        var bytes = variance.ToByteArray();
        var bitLength = (bytes.Length - 1) * 8;
        for (var top = bytes[bytes.Length - 1]; top != 0; top >>= 1) bitLength++;
        var shift = Math.Max(0, (bitLength - 256) / 2);
        var leading = variance >> (2 * shift);
        var root = BigInteger.One << 129;
        while (true)
        {
            var next = (root + leading / root) / 2;
            if (next >= root) break;
            root = next;
        }
        var lowerRoot = root << shift;
        var upperRoot = (root + 1) << shift;
        var scaledDenominator = denominator << 1074;
        var lower = ReferenceFraction.RatioToDouble(center + (direction > 0 ? lowerRoot : -upperRoot), scaledDenominator);
        var upper = ReferenceFraction.RatioToDouble(center + (direction > 0 ? upperRoot : -lowerRoot), scaledDenominator);
        if (lower.Equals(upper)) return lower; // NOSONAR: S1244 - Equal rounded enclosure endpoints certify one binary64 result; a tolerance would not.
        var fourVariance = 4 * variance;
        int Compare(BigInteger twiceUnits)
        {
            var distance = (twiceUnits * denominator - 2 * center) * direction;
            if (distance.Sign < 0) return direction;
            return direction * fourVariance.CompareTo(distance * distance);
        }
        var negative = Compare(BigInteger.Zero) < 0;
        if (negative) { center = -center; direction = -direction; }
        long low = 0, high = 0x7fefffffffffffff;
        while (low < high)
        {
            var candidate = low + (high - low + 1) / 2;
            if (Compare(2 * KaufmanReferenceUnits(BitConverter.Int64BitsToDouble(candidate))) >= 0) low = candidate;
            else high = candidate - 1;
        }
        var a = KaufmanReferenceUnits(BitConverter.Int64BitsToDouble(low));
        var b = low == 0x7fefffffffffffff ? BigInteger.One << 2098
            : KaufmanReferenceUnits(BitConverter.Int64BitsToDouble(low + 1));
        var comparison = Compare(a + b);
        var result = BitConverter.Int64BitsToDouble(comparison < 0 || comparison == 0 && (low & 1) == 0 ? low : low + 1);
        return negative ? -result : result;
    }
}
