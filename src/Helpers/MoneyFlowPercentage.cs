#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact publication for the certified Int64 MFI recurrence. The factor 100
// needs at most 70 numerator bits; the denominator needs at most 64. Their
// quotient is always normal, and its 53-bit rounding division fits UInt128.
internal static class MoneyFlowPercentage
{
    internal static double Calculate(long positive, long total)
    {
        if (positive == 0 || total == 0) return 0;
        bool negative = (positive < 0) != (total < 0);
        ulong magnitude = positive < 0 ? unchecked((ulong)-positive) : (ulong)positive;
        ulong denominator = total < 0 ? unchecked((ulong)-total) : (ulong)total;
        UInt128 numerator = unchecked((UInt128)magnitude * 100);
        ulong upper = (ulong)(numerator >> 64);
        // Retain hardware floating-point division for exact small operands.
        // Only larger numerators need the integer rounding-grid division.
        const ulong exactLimit = 1UL << 53;
        if (upper == 0 && numerator <= exactLimit && denominator <= exactLimit)
        {
            double value = (double)(ulong)numerator / denominator;
            return negative ? -value : value;
        }
        int numeratorBits = upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)numerator)
            : 128 - BitOperations.LeadingZeroCount(upper);
        int power = numeratorBits - (64 - BitOperations.LeadingZeroCount(denominator));
        if (power >= 0 ? numerator < ((UInt128)denominator << power)
            : (numerator << -power) < denominator) power--;
        int shift = 52 - power;
        // A left-shifted numerator has at most 117 bits. When shift is
        // negative, the shifted denominator has at most 18 bits.
        if (shift >= 0) numerator <<= shift;
        else denominator <<= -shift;
        var (quotient, remainder) = ExactMeanAccumulator.CompactDivRem(numerator, denominator);
        // The normalized quotient has 53 bits and the remainder is smaller
        // than the UInt64 denominator; the rounding decision stays narrow.
        ulong rounded = unchecked((ulong)quotient), residual = unchecked((ulong)remainder);
        ulong complement = denominator - residual;
        if (residual > complement || residual == complement && (rounded & 1) != 0) rounded++;
        // Encode handles a carry to the next binade. No floating-point
        // estimate, operand rounding, or second scaling round is involved.
        return ExactMeanAccumulator.Encode(rounded, power + 1022, negative);
    }
}
#endif
