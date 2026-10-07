using System.Numerics;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Independent fixed-grid oracle: each finite binary64 input is an integer number
// of 2^-1074 units. Products therefore occupy the 2^-2148 grid. This does not call
// the production rolling accumulator or its rational validation reference.
internal static class PenetrationReferenceArithmetic
{
    internal static bool Passes(
        double firstOpen,
        double firstClose,
        double currentClose,
        double penetration,
        bool up,
        bool competitorArithmetic
    )
    {
        if (competitorArithmetic)
        {
            var product = Math.Abs(firstClose - firstOpen) * penetration;
            return up ? currentClose > firstClose + product : currentClose < firstClose - product;
        }
        var distance = (Units(currentClose) - Units(firstClose)) * (up ? 1 : -1);
        var body = BigInteger.Abs(Units(firstClose) - Units(firstOpen));
        return (distance << 1074) > body * Units(penetration);
    }

    internal static BigInteger Units(double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        var exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047)
            throw new ArgumentOutOfRangeException(nameof(value));
        var mantissa = new BigInteger(bits & 0xfffffffffffffUL);
        if (exponent != 0)
            mantissa += BigInteger.One << 52;
        var magnitude = mantissa << Math.Max(0, exponent - 1);
        return (bits >> 63) == 0 ? magnitude : -magnitude;
    }
}
