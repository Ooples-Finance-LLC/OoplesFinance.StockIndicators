using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Round log(1+x) once. Close log returns can differ by much less than a log's
// own ulp, so an ordinary log1p approximation is unsafe before a variance.
internal static class RoundedNearUnityLog
{
    internal static double Of(double current, double previous)
    {
#pragma warning disable S1244 // Exact equality identifies zero change or identical rounded interval endpoints.
        if (current == previous) return 0;
#pragma warning restore S1244
        var denominator = ExactVarianceWindow.Units(previous);
        var numerator = ExactVarianceWindow.Units(current) - denominator;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        numerator /= gcd;
        denominator /= gcd;
        var magnitude = BigInteger.Abs(numerator);
        BigInteger powerNumerator = numerator, powerDenominator = denominator;
        BigInteger sumNumerator = 0, sumDenominator = 1;
        for (var degree = 1; ; degree++)
        {
            var termNumerator = (degree & 1) == 1 ? powerNumerator : -powerNumerator;
            var termDenominator = degree * powerDenominator;
            sumNumerator = sumNumerator * termDenominator + termNumerator * sumDenominator;
            sumDenominator *= termDenominator;
            gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(sumNumerator), sumDenominator);
            sumNumerator /= gcd;
            sumDenominator /= gcd;

            powerNumerator *= numerator;
            powerDenominator *= denominator;
            // Absolute tail <= |x|^(degree+1) / ((degree+1)*(1-|x|)).
            var tailNumerator = BigInteger.Abs(powerNumerator) * denominator;
            var tailDenominator = (degree + 1) * powerDenominator * (denominator - magnitude);
            var common = sumDenominator * tailDenominator;
            var center = sumNumerator * tailDenominator;
            var radius = tailNumerator * sumDenominator;
            var lower = ExactMeanAccumulator.UnitRatio((center - radius) << 1074, common);
            var upper = ExactMeanAccumulator.UnitRatio((center + radius) << 1074, common);
#pragma warning disable S1244 // Exact equality identifies zero change or identical rounded interval endpoints.
            if (lower == upper) return lower;
#pragma warning restore S1244
        }
    }
}
