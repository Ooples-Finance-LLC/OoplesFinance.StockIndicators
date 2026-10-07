using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Positive 53-bit arithmetic for unpublished strength averages. Unlike binary64,
// the exponent extends below the subnormal floor as well as above the upper bound.
// Seed coefficients have at most 2130 bits, weighted differences at most 2100, and a
// rounded mantissa times a period at most 84. Keeping 4096 bits therefore retains
// the dominant addend exactly; at most one subordinate positive tail is discarded.
// That tail is below one retained unit and only changes a rounding midpoint.
internal readonly record struct PositiveStrengthValue(BigInteger Mantissa, long Exponent)
{
    internal bool IsZero => Mantissa.IsZero;

    internal static PositiveStrengthValue Seed(BigInteger units, int period) =>
        Combine(units, -1074, 0, 0, period);

    internal PositiveStrengthValue Update(BigInteger change, int period) =>
        Combine(Mantissa * (period - 1), Exponent, change, -1074, period);

    internal PositiveStrengthValue UpdateExponential(BigInteger change, int period) =>
        Combine(Mantissa * (period - 1), Exponent, 2 * change, -1074, (long)period + 1);

    internal static double Quotient(
        PositiveStrengthValue numerator,
        PositiveStrengthValue denominator
    )
    {
        if (denominator.IsZero)
            throw new DivideByZeroException();
        if (numerator.IsZero)
            return 0;
        var gap =
            numerator.Exponent
            + numerator.Mantissa.GetBitLength()
            - denominator.Exponent
            - denominator.Mantissa.GetBitLength();
        if (gap > 4096)
            return double.PositiveInfinity;
        if (gap < -4096)
            return 0;
        var exponent = Math.Min(numerator.Exponent, denominator.Exponent);
        var top = numerator.Mantissa << (int)(numerator.Exponent - exponent);
        var bottom = denominator.Mantissa << (int)(denominator.Exponent - exponent);
        return ExactMeanAccumulator.UnitRatio(top << 1074, bottom);
    }

    private static PositiveStrengthValue Combine(
        BigInteger a,
        long aExponent,
        BigInteger b,
        long bExponent,
        long divisor
    )
    {
        if (a.IsZero && b.IsZero)
            return default;
        var top = Math.Max(
            a.IsZero ? long.MinValue : aExponent + a.GetBitLength(),
            b.IsZero ? long.MinValue : bExponent + b.GetBitLength()
        );
        var bottom =
            a.IsZero ? bExponent
            : b.IsZero ? aExponent
            : Math.Min(aExponent, bExponent);
        var cut = Math.Max(bottom, top - 4096);
        var sticky = false;
        BigInteger Align(BigInteger value, long exponent)
        {
            if (value.IsZero)
                return 0;
            if (exponent >= cut)
                return value << (int)(exponent - cut);
            var shift = cut - exponent;
            if (shift >= value.GetBitLength())
            {
                sticky = true;
                return 0;
            }
            var truncated = value >> (int)shift;
            sticky |= value != (truncated << (int)shift);
            return truncated;
        }
        var numerator = Align(a, aExponent) + Align(b, bExponent);
        BigInteger denominator = divisor;
        var exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (
            exponent >= 0
                ? numerator < (denominator << exponent)
                : (numerator << -exponent) < denominator
        )
            exponent--;
        var quantum = exponent - 52;
        if (quantum < 0)
            numerator <<= -quantum;
        else
            denominator <<= quantum;
        var mantissa = BigInteger.DivRem(numerator, denominator, out var remainder);
        var half = (2 * remainder).CompareTo(denominator);
        if (half > 0 || half == 0 && (sticky || !mantissa.IsEven))
            mantissa++;
        var resultExponent = cut + quantum;
        if (mantissa.GetBitLength() > 53)
        {
            mantissa >>= 1;
            resultExponent++;
        }
        return new(mantissa, resultExponent);
    }

    internal static double Percent(
        PositiveStrengthValue gain,
        PositiveStrengthValue loss,
        bool signed
    )
    {
        if (gain.IsZero)
            return loss.IsZero ? 0
                : signed ? -100
                : 0;
        if (loss.IsZero)
            return 100;
        var gap =
            gain.Exponent
            + gain.Mantissa.GetBitLength()
            - loss.Exponent
            - loss.Mantissa.GetBitLength();
        // Beyond this gap the small term cannot affect any binary64 result,
        // including the RSI subnormal rounding boundary or either CMO endpoint.
        if (gap > 4096)
            return 100;
        if (gap < -4096)
            return signed ? -100 : 0;
        var exponent = Math.Min(gain.Exponent, loss.Exponent);
        var up = gain.Mantissa << (int)(gain.Exponent - exponent);
        var down = loss.Mantissa << (int)(loss.Exponent - exponent);
        // UnitRatio accepts a numerator on the binary64 subnormal grid.
        return ExactMeanAccumulator.UnitRatio((100 * (signed ? up - down : up)) << 1074, up + down);
    }
}
