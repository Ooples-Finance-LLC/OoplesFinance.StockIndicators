using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Positive 53-bit arithmetic for unpublished strength averages. Unlike binary64,
// the exponent extends below the subnormal floor as well as above the upper bound.
// Seed coefficients have at most 2130 bits, weighted differences at most 2100, and a
// rounded mantissa times a period at most 84. Keeping 4096 bits therefore retains
// the dominant addend exactly; at most one subordinate positive tail is discarded.
// That tail is below one retained unit and only changes a rounding midpoint.
internal readonly record struct PositiveStrengthValue(long Mantissa, long Exponent)
{
    internal bool IsZero => Mantissa == 0;

    internal static PositiveStrengthValue Seed(BigInteger units, int period) =>
        Combine(units, -1074, 0, 0, period);

    internal static PositiveStrengthValue Seed(ExactMeanAccumulator sum, int period)
    {
        var (rounded, exponent) = sum.NormalizedMean(period);
        if (rounded == 0) return default;
        var (mantissa, power) = ExactMeanAccumulator.DecomposeFinite(rounded);
        var padding = 53 - Bits(mantissa);
        return new(mantissa << padding, (long)exponent + power - padding);
    }

    internal PositiveStrengthValue Update(BigInteger change, int period) =>
        Combine((BigInteger)Mantissa * (period - 1), Exponent, change, -1074, period);

    internal PositiveStrengthValue UpdateExponential(BigInteger change, int period) =>
        Combine((BigInteger)Mantissa * (period - 1), Exponent, 2 * change, -1074, (long)period + 1);

    internal static bool TryDifference(double current, double previous, out double difference)
    {
#if !NETFRAMEWORK
        var exact = JurikDyadic.FromDouble(current);
        exact.Subtract(JurikDyadic.FromDouble(previous));
        return exact.TryDouble(out difference);
#else
        difference = 0;
        return false;
#endif
    }

    internal PositiveStrengthValue Update(double change, int period, bool exponential = false)
    {
        var (integer, exponent) = ExactMeanAccumulator.DecomposeFinite(change);
        int priorWeight = period - 1;
        long divisor = exponential ? (long)period + 1 : period;
        bool hasPrior = Mantissa != 0 && priorWeight != 0;
        if (hasPrior && integer != 0 && (Exponent < exponent - 64L || Exponent > exponent + 64L))
            return exponential ? UpdateExponential(ExactVarianceWindow.Units(change), period)
                : Update(ExactVarianceWindow.Units(change), period);
        if (!hasPrior && integer == 0) return default;
        long common = !hasPrior ? exponent : integer == 0 ? Exponent : Math.Min(Exponent, exponent);
        int priorShift = hasPrior ? (int)(Exponent - common) : 0;
        int currentShift = integer != 0 ? (int)(exponent - common) : 0;
        double rounded;
        if (TryWeightedTerm(hasPrior ? Mantissa : 0, priorWeight, priorShift, out var prior)
            && TryWeightedTerm(integer, exponential ? 2 : 1, currentShift, out var current)
            && prior <= long.MaxValue - current)
        {
            rounded = ExactMeanAccumulator.ScaledRatio(prior + current, divisor, 0);
        }
        else
        {
            var sum = hasPrior ? JurikDyadic.FromDouble(Mantissa) : default;
            if (hasPrior)
            {
                sum.ScaleByPowerOfTwo(priorShift);
                sum.Multiply(priorWeight);
            }
            if (integer != 0)
            {
                var term = JurikDyadic.FromDouble(integer);
                term.ScaleByPowerOfTwo(currentShift);
                if (exponential) term.Multiply(2);
                sum.AddExact(term);
            }
            rounded = sum.Ratio(JurikDyadic.FromDouble(divisor));
        }
        // Round only after normalizing the common exponent away. This keeps
        // positive means alive below binary64's subnormal floor.
        var (mantissa, power) = ExactMeanAccumulator.DecomposeFinite(rounded);
        var padding = 53 - Bits(mantissa);
        return new(mantissa << padding, common + power - padding);
    }

    private static bool TryWeightedTerm(long mantissa, int weight, int shift, out long value)
    {
        value = 0;
        if (mantissa == 0 || weight == 0) return true;
        // A conservative bit bound proves both multiplication and shift fit;
        // the caller separately checks the addition before evaluating it.
        if (Bits(mantissa) + Bits(weight) + shift > 63) return false;
        value = (mantissa * weight) << shift;
        return true;
    }

    private static int Bits(long value)
    {
#if !NETFRAMEWORK
        return 64 - BitOperations.LeadingZeroCount((ulong)value);
#else
        int bits = 0;
        while (value != 0) { bits++; value >>= 1; }
        return bits;
#endif
    }

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
            + Bits(numerator.Mantissa)
            - denominator.Exponent
            - Bits(denominator.Mantissa);
        if (gap > 4096)
            return double.PositiveInfinity;
        if (gap < -4096)
            return 0;
        var exponent = Math.Min(numerator.Exponent, denominator.Exponent);
        return Exact(numerator, exponent).Ratio(Exact(denominator, exponent));
    }

    private static JurikDyadic Exact(PositiveStrengthValue value, long commonExponent)
    {
        // Published ratios cancel the absolute exponent, including exponents
        // below the binary64 floor. Stored mantissas have at most 53 bits.
        var result = JurikDyadic.FromDouble((double)(long)value.Mantissa);
        var shift = (int)(value.Exponent - commonExponent);
        if (shift != 0) result.ScaleByPowerOfTwo(shift);
        return result;
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
            a.IsZero ? long.MinValue : aExponent + FrameworkCompatibility.GetBitLength(a),
            b.IsZero ? long.MinValue : bExponent + FrameworkCompatibility.GetBitLength(b)
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
            if (shift >= FrameworkCompatibility.GetBitLength(value))
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
        var exponent = (int)(FrameworkCompatibility.GetBitLength(numerator) - FrameworkCompatibility.GetBitLength(denominator));
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
        if (FrameworkCompatibility.GetBitLength(mantissa) > 53)
        {
            mantissa >>= 1;
            resultExponent++;
        }
        return new((long)mantissa, resultExponent);
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
            + Bits(gain.Mantissa)
            - loss.Exponent
            - Bits(loss.Mantissa);
        // Beyond this gap the small term cannot affect any binary64 result,
        // including the RSI subnormal rounding boundary or either CMO endpoint.
        if (gap > 4096)
            return 100;
        if (gap < -4096)
            return signed ? -100 : 0;
        var exponent = Math.Min(gain.Exponent, loss.Exponent);
        var up = Exact(gain, exponent);
        var down = Exact(loss, exponent);
        var denominator = up;
        denominator.AddExact(down);
        if (signed) up.Subtract(down);
        up.Multiply(100);
        return up.Ratio(denominator);
    }
}
