using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> KaufmanBinaryValues(IReadOnlyList<Bar> bars, int length, double fast, double slow, double filter)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var zero = R(0);
        var prices = bars.Select(b => Helpers.ExactVarianceWindow.Units(b.Close)).ToArray();
        var averages = new BigInteger[prices.Length]; var changes = new BigInteger[prices.Length];
        var denominators = new BigInteger[prices.Length];
        var result = new double[prices.Length]; var lastFall = -1; var lastRise = -1;
        var threshold = (R(filter) / R(100)).Components;
        var distanceScale = threshold.Denominator * threshold.Denominator * length * length * length;
        for (var i = 0; i < prices.Length; i++)
        {
            var travel = BigInteger.Zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += BigInteger.Abs(prices[j] - prices[j - 1]);
            var efficiency = travel.IsZero ? zero : new ReferenceFraction(BigInteger.Abs(prices[i] - prices[i - length])) / new ReferenceFraction(travel);
            var coefficient = efficiency * R(fast) + R(slow); var gain = coefficient * coefficient;
            var weight = gain.Components;
            var priorDenominator = i == 0 ? BigInteger.One : denominators[i - 1];
            var priorNumerator = i == 0 ? prices[i] : averages[i - 1];
            denominators[i] = priorDenominator * weight.Denominator;
            averages[i] = weight.Numerator * prices[i] * priorDenominator
                + (weight.Denominator - weight.Numerator) * priorNumerator;
            changes[i] = averages[i] - priorNumerator * weight.Denominator;
            // Every denominator divides subsequent denominators. Integer
            // coordinates preserve exact centered squares without repeated GCDs.
            var variance = BigInteger.Zero;
            if (i + 1 >= length)
            {
                var coordinates = new BigInteger[length]; var sum = BigInteger.Zero;
                for (var j = 0; j < length; j++)
                {
                    var index = i - length + 1 + j;
                    coordinates[j] = changes[index] * (denominators[i] / denominators[index]); sum += coordinates[j];
                }
                foreach (var value in coordinates)
                { var centered = value * length - sum; variance += centered * centered; }
            }
            if (changes[i].Sign < 0) lastFall = i;
            if (changes[i].Sign > 0) lastRise = i;
            var thresholdSquared = threshold.Numerator * threshold.Numerator * variance;
            BigInteger At(int index) => index < 0 ? BigInteger.Zero : averages[index] * (denominators[i] / denominators[index]);
            bool Clears(BigInteger distance)
            {
                if (thresholdSquared.Sign == 0) return distance.Sign > 0;
                if (filter > 0) return distance.Sign > 0 && (distance * distance * distanceScale).CompareTo(thresholdSquared) > 0;
                if (distance.Sign >= 0) return true;
                return (distance * distance * distanceScale).CompareTo(thresholdSquared) < 0;
            }
            result[i] = Clears(averages[i] - At(lastFall)) ? 1 : Clears(At(lastRise) - averages[i]) ? -1 : 0;
        }
        return new() { ["Kbw"] = result };
    }

}
