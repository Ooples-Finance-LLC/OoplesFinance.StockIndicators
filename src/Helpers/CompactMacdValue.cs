using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact integer subnormal-grid units, with the common binary scale kept out
// of the coefficient. Uncertified ratios retain the original wide arithmetic.
internal readonly struct CompactMacdValue
{
    private readonly BigInteger _coefficient;
    private readonly int _scale;
#if !NETFRAMEWORK
    private readonly Int128 _compact;
    private readonly bool _wide;
    private CompactMacdValue(Int128 coefficient, int scale)
    {
        _compact = coefficient; _scale = coefficient == 0 ? 0 : scale;
        _coefficient = default; _wide = false;
    }
    private static UInt128 Magnitude(Int128 value) => value < 0
        ? (UInt128)(-(value + 1)) + 1 : (UInt128)value;
    private static int Width(UInt128 value)
    {
        ulong high = (ulong)(value >> 64);
        return high != 0 ? 128 - BitOperations.LeadingZeroCount(high)
            : 64 - BitOperations.LeadingZeroCount((ulong)value);
    }
#endif
    private CompactMacdValue(BigInteger coefficient, int scale)
    {
        _scale = coefficient.IsZero ? 0 : scale;
#if !NETFRAMEWORK
        if (BigInteger.Abs(coefficient).GetBitLength() <= 127)
        {
            _compact = (Int128)coefficient; _coefficient = default; _wide = false;
            return;
        }
        _compact = default; _wide = true;
#endif
        _coefficient = coefficient;
    }
    private BigInteger Coefficient
    {
        get
        {
#if !NETFRAMEWORK
            if (!_wide) return (BigInteger)_compact;
#endif
            return _coefficient;
        }
    }
    internal bool IsZero
    {
        get
        {
#if !NETFRAMEWORK
            if (!_wide) return _compact == 0;
#endif
            return _coefficient.IsZero;
        }
    }
    internal BigInteger Units => Coefficient << _scale;
    internal static CompactMacdValue Of(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
        long coefficient = bits & ((1L << 52) - 1);
        if (exponent != 0) coefficient |= 1L << 52;
#if !NETFRAMEWORK
        return new((Int128)(bits < 0 ? -coefficient : coefficient), exponent == 0 ? 0 : exponent - 1);
#else
        return new(new BigInteger(bits < 0 ? -coefficient : coefficient), exponent == 0 ? 0 : exponent - 1);
#endif
    }
    internal static CompactMacdValue FromUnits(BigInteger units)
    {
#if !NETFRAMEWORK
        if (!units.IsZero)
        {
            int scale = checked((int)BigInteger.TrailingZeroCount(BigInteger.Abs(units)));
            return new(units >> scale, scale);
        }
#endif
        return new(units, 0);
    }
    public static CompactMacdValue operator +(CompactMacdValue left, CompactMacdValue right)
    {
        if (left.IsZero) return right;
        if (right.IsZero) return left;
        int scale = Math.Min(left._scale, right._scale);
#if !NETFRAMEWORK
        int leftShift = left._scale - scale, rightShift = right._scale - scale;
        if (!left._wide && !right._wide
            && Width(Magnitude(left._compact)) + leftShift <= 127
            && Width(Magnitude(right._compact)) + rightShift <= 127)
        {
            // Shifts are guarded separately: checked shifts do not detect lost bits.
            try { return new(checked((left._compact << leftShift) + (right._compact << rightShift)), scale); }
            catch (OverflowException) { } // Exact wide fallback below.
        }
#endif
        return new((left.Coefficient << (left._scale - scale))
            + (right.Coefficient << (right._scale - scale)), scale);
    }
    public static CompactMacdValue operator -(CompactMacdValue left, CompactMacdValue right)
    {
#if !NETFRAMEWORK
        if (!right._wide && right._compact != Int128.MinValue)
            return left + new CompactMacdValue(-right._compact, right._scale);
#endif
        return left + new CompactMacdValue(-right.Coefficient, right._scale);
    }
    internal CompactMacdValue Times(BigInteger weight)
    {
#if !NETFRAMEWORK
        if (!_wide && BigInteger.Abs(weight).GetBitLength() <= 127)
        {
            try { return new(checked(_compact * (Int128)weight), _scale); }
            catch (OverflowException) { } // Exact wide fallback below.
        }
#endif
        return new(Coefficient * weight, _scale);
    }
    internal CompactMacdValue Round(BigInteger divisor)
    {
#if !NETFRAMEWORK
        if (!_wide && divisor.Sign > 0 && divisor.GetBitLength() <= 128
            && TryRatio(_compact, (UInt128)divisor, _scale, out var compact)) return compact;
#endif
        return Ratio(Coefficient, divisor, _scale);
    }
    internal CompactMacdValue Percentage(CompactMacdValue divisor)
    {
        int scale = checked(_scale - divisor._scale + 1074);
#if !NETFRAMEWORK
        if (!_wide && !divisor._wide && divisor._compact != 0)
        {
            try
            {
                Int128 numerator = checked(100 * _compact * (divisor._compact < 0 ? -1 : 1));
                if (TryRatio(numerator, Magnitude(divisor._compact), scale, out var compact)) return compact;
            }
            catch (OverflowException) { } // Exact wide fallback below.
        }
#endif
        return Ratio(100 * Coefficient * divisor.Coefficient.Sign, BigInteger.Abs(divisor.Coefficient), scale);
    }
#if !NETFRAMEWORK
    private static bool TryRatio(Int128 numerator, UInt128 denominator, int scale, out CompactMacdValue result)
    {
        result = default;
        if (numerator == 0) return denominator != 0;
        if (!CertifiedCompactRatio.TryDivide(Magnitude(numerator), denominator,
            checked(scale - 1074), out double rounded)) return false;
        result = Of(numerator < 0 ? -rounded : rounded);
        return true;
    }
#endif
    private static CompactMacdValue Ratio(BigInteger numerator, BigInteger denominator, int scale)
    {
        if (denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        if (numerator.IsZero) return default;
#if !NETFRAMEWORK
        var magnitude = BigInteger.Abs(numerator);
        if (magnitude.GetBitLength() <= 128 && denominator.GetBitLength() <= 128
            && CertifiedCompactRatio.TryDivide((UInt128)magnitude, (UInt128)denominator,
                checked(scale - 1074), out double rounded))
            return Of(numerator.Sign < 0 ? -rounded : rounded);
#endif
        // This also preserves extended unpublished values and exact subnormal
        // rounding. Never publish a finite approximation for an unproven ratio.
        return FromUnits(scale >= 0
            ? RocBankValue.RoundUnits(numerator << scale, denominator)
            : RocBankValue.RoundUnits(numerator, denominator << -scale));
    }
    internal double Publish()
    {
#if !NETFRAMEWORK
        if (!_wide)
        {
            UInt128 magnitude = Magnitude(_compact);
            if (Width(magnitude) <= 53)
            {
                double value = Math.ScaleB((double)(ulong)magnitude, _scale - 1074);
                if (!double.IsInfinity(value)) return _compact < 0 ? -value : value;
            }
        }
#endif
        return ExactMeanAccumulator.UnitRatio(Units, 1);
    }
}
