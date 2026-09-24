namespace OoplesFinance.StockIndicators.Helpers;

internal static class StableLogRatio
{
    internal static double Of(double current, double previous)
    {
        if (current <= 0 || previous <= 0) return 0;
        var quotient = current / previous;
        if (quotient >= 0.5 && quotient <= 2)
        {
            // Sterbenz subtraction is exact here. Correct the rounded 1+r argument
            // so adjacent prices retain their small nonzero logarithmic return.
            var relative = (current - previous) / previous;
            var argument = 1 + relative;
            return argument == 1 ? relative : Math.Log(argument) * (relative / (argument - 1)); // NOSONAR: S1244 - Detect exact rounding to one before dividing by argument minus one.
        }
        var (currentMantissa, currentExponent) = Decompose(current);
        var (previousMantissa, previousExponent) = Decompose(previous);
        return Math.Log(currentMantissa / previousMantissa)
            + (currentExponent - previousExponent) * Math.Log(2);
    }

    private static (double Mantissa, int Exponent) Decompose(double value)
    {
        var correction = 0;
        if (value < 2.2250738585072014e-308) { value *= 18014398509481984d; correction = -54; }
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047) - 1023 + correction;
        var mantissa = BitConverter.Int64BitsToDouble((bits & 0x000fffffffffffffL) | 0x3ff0000000000000L);
        return (mantissa, exponent);
    }
}
