using System.Runtime.CompilerServices;
using Big = System.Numerics.BigInteger;

namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>
/// An arbitrary-precision integer with exactly <see cref="Big"/>'s integer semantics, stored as an odd mantissa times
/// a power of two.
/// </summary>
/// <remarks>
/// <para>The exact windows hold binary64 inputs as integer multiples of 2^-1074 (<see cref="ExactVarianceWindow.Units"/>),
/// so an ordinary price is a ~1,080-bit integer whose low ~1,030 bits are zero. As a plain <see cref="Big"/> every add,
/// multiply and compare walks all of those zero words; here the zeros live in <see cref="_exponent"/> and arithmetic
/// runs on the ~53-bit significands.</para>
/// <para>Every operation returns the same integer <see cref="Big"/> would, so results are bit-identical; the
/// representation is normalized (odd mantissa, or zero with exponent 0), so structural equality is value equality.
/// Members without a cheap exact form (bitwise operators, <c>ToByteArray</c>, <c>Log</c>, <c>ModPow</c>, narrowing
/// conversions) convert to <see cref="Big"/> and delegate.</para>
/// </remarks>
internal readonly struct ExactInteger : IComparable, IComparable<ExactInteger>, IEquatable<ExactInteger>, IFormattable
{
    private readonly Big _mantissa; // odd, or zero
    private readonly int _exponent; // >= 0; zero when _mantissa is zero

    private ExactInteger(Big oddMantissa, int exponent, bool _)
    {
        _mantissa = oddMantissa;
        _exponent = oddMantissa.IsZero ? 0 : exponent;
    }

    public ExactInteger(Big value)
    {
        if (value.IsZero) { _mantissa = value; _exponent = 0; return; }
        var zeros = TrailingZeros(value);
        _mantissa = zeros == 0 ? value : value >> zeros;
        _exponent = zeros;
    }

    public ExactInteger(int value) : this((long)value) { }
    public ExactInteger(uint value) : this((long)value) { }

    public ExactInteger(long value)
    {
        if (value == 0 || value == long.MinValue) { this = new ExactInteger((Big)value); return; }
        var zeros = TrailingZeros(value);
        _mantissa = value >> zeros;
        _exponent = zeros;
    }
    public ExactInteger(ulong value) : this((Big)value) { }
    public ExactInteger(double value) : this(new Big(value)) { }
    public ExactInteger(float value) : this(new Big(value)) { }
    public ExactInteger(decimal value) : this(new Big(value)) { }
    public ExactInteger(byte[] value) : this(new Big(value)) { }

    /// <summary>The value times 2^<paramref name="shift"/>, normalizing <paramref name="mantissa"/> as needed.</summary>
    private static ExactInteger Make(Big mantissa, int shift)
    {
        if (mantissa.IsZero) return default;
        if (!mantissa.IsEven) return new ExactInteger(mantissa, shift, true);
        var zeros = TrailingZeros(mantissa);
        return new ExactInteger(mantissa >> zeros, checked(shift + zeros), true);
    }

    public static ExactInteger Zero => default;
    public static ExactInteger One => new(Big.One, 0, true);
    public static ExactInteger MinusOne => new(Big.MinusOne, 0, true);

    public int Sign => _mantissa.Sign;
    public bool IsZero => _mantissa.IsZero;
    public bool IsOne => _exponent == 0 && _mantissa.IsOne;
    public bool IsEven => _exponent > 0 || _mantissa.IsEven;
    public bool IsPowerOfTwo => _mantissa.IsOne;

    /// <summary>The same value as a <see cref="Big"/>.</summary>
    public Big ToBig() => _exponent == 0 ? _mantissa : _mantissa << _exponent;

    // ---- conversions ----
    public static implicit operator ExactInteger(Big value) => new(value);
    public static implicit operator Big(ExactInteger value) => value.ToBig();
    public static implicit operator ExactInteger(byte value) => new((long)value);
    public static implicit operator ExactInteger(sbyte value) => new((long)value);
    public static implicit operator ExactInteger(short value) => new((long)value);
    public static implicit operator ExactInteger(ushort value) => new((long)value);
    public static implicit operator ExactInteger(int value) => new((long)value);
    public static implicit operator ExactInteger(uint value) => new((long)value);
    public static implicit operator ExactInteger(long value) => new(value);
    public static implicit operator ExactInteger(ulong value) => new((Big)value);
    public static explicit operator ExactInteger(double value) => new((Big)value);
    public static explicit operator ExactInteger(float value) => new((Big)value);
    public static explicit operator ExactInteger(decimal value) => new((Big)value);

    public static explicit operator double(ExactInteger value)
    {
        // A mantissa below 2^53 converts exactly, and scaling by a power of two is exact up to overflow, where both
        // this and Big's conversion give the signed infinity.
        if (value._exponent == 0) return (double)value._mantissa;
        if (BitLength(Big.Abs(value._mantissa)) <= 53)
        {
#if NETFRAMEWORK
            return (double)value.ToBig();
#else
            return Math.ScaleB((double)value._mantissa, value._exponent);
#endif
        }
        return (double)value.ToBig();
    }
    public static explicit operator float(ExactInteger value) => (float)value.ToBig();
    public static explicit operator decimal(ExactInteger value) => (decimal)value.ToBig();
    public static explicit operator long(ExactInteger value)
    {
        if (value._exponent == 0) return (long)value._mantissa;
        // |m|·2^e < 2^63 fits; anything wider (including long.MinValue) takes Big's checked conversion.
        if (value._exponent < 63 && BitLength(Big.Abs(value._mantissa)) + value._exponent <= 62) return (long)value._mantissa << value._exponent;
        return (long)value.ToBig();
    }
    public static explicit operator ulong(ExactInteger value) => (ulong)value.ToBig();
    public static explicit operator int(ExactInteger value) => (int)value.ToBig();
    public static explicit operator uint(ExactInteger value) => (uint)value.ToBig();
    public static explicit operator short(ExactInteger value) => (short)value.ToBig();
    public static explicit operator ushort(ExactInteger value) => (ushort)value.ToBig();
    public static explicit operator byte(ExactInteger value) => (byte)value.ToBig();
    public static explicit operator sbyte(ExactInteger value) => (sbyte)value.ToBig();

#if NET8_0_OR_GREATER
    public static explicit operator ExactInteger(Int128 value) => new((Big)value);
    public static explicit operator ExactInteger(UInt128 value) => new((Big)value);
    public static explicit operator Int128(ExactInteger value) => (Int128)value.ToBig();
    public static explicit operator UInt128(ExactInteger value) => (UInt128)value.ToBig();
#endif

    // ---- arithmetic ----
    public static ExactInteger operator +(ExactInteger value) => value;
    public static ExactInteger operator -(ExactInteger value) => new(-value._mantissa, value._exponent, true);

    public static ExactInteger operator +(ExactInteger left, ExactInteger right)
    {
        if (left.IsZero) return right;
        if (right.IsZero) return left;
        if (left._exponent == right._exponent) return Make(left._mantissa + right._mantissa, left._exponent);
        // Unequal exponents: the lower-exponent mantissa is odd and the shifted one even, so the sum stays odd.
        return left._exponent < right._exponent
            ? new ExactInteger((right._mantissa << (right._exponent - left._exponent)) + left._mantissa, left._exponent, true)
            : new ExactInteger((left._mantissa << (left._exponent - right._exponent)) + right._mantissa, right._exponent, true);
    }

    public static ExactInteger operator -(ExactInteger left, ExactInteger right) => left + -right;

    public static ExactInteger operator *(ExactInteger left, ExactInteger right)
    {
        if (left.IsZero || right.IsZero) return default;
        // odd * odd is odd: no normalization.
        return new ExactInteger(left._mantissa * right._mantissa, checked(left._exponent + right._exponent), true);
    }

    /// <summary>Truncating division, as <see cref="Big"/>'s.</summary>
    public static ExactInteger operator /(ExactInteger dividend, ExactInteger divisor)
    {
        if (divisor.IsZero) throw new DivideByZeroException();
        if (dividend.IsZero) return default;
        if (divisor._mantissa.IsOne || divisor._mantissa == Big.MinusOne)
        {
            var quotient = TruncateByPowerOfTwo(dividend, divisor._exponent);
            return divisor.Sign < 0 ? -quotient : quotient;
        }
        if (dividend._exponent >= divisor._exponent)
        {
            // m1·2^e1 / (m2·2^e2) = (m1·2^(e1-e2)) / m2 exactly; the odd divisor shares no factor of two.
            var shift = dividend._exponent - divisor._exponent;
            return Make(Big.Divide(shift == 0 ? dividend._mantissa : dividend._mantissa << shift, divisor._mantissa), 0);
        }
        return Make(Big.Divide(dividend._mantissa, divisor._mantissa << (divisor._exponent - dividend._exponent)), 0);
    }

    public static ExactInteger operator %(ExactInteger dividend, ExactInteger divisor) => dividend - dividend / divisor * divisor;

    public static ExactInteger operator ++(ExactInteger value) => value + One;
    public static ExactInteger operator --(ExactInteger value) => value - One;

    /// <summary>trunc(value / 2^shift).</summary>
    private static ExactInteger TruncateByPowerOfTwo(ExactInteger value, int shift)
    {
        if (shift <= value._exponent) return new ExactInteger(value._mantissa, value._exponent - shift, true);
        var drop = shift - value._exponent;
        var magnitude = Big.Abs(value._mantissa) >> drop;
        return Make(value.Sign < 0 ? -magnitude : magnitude, 0);
    }

    public static ExactInteger operator <<(ExactInteger value, int shift)
    {
        if (shift < 0) return value >> -shift;
        if (value.IsZero) return default;
        return new ExactInteger(value._mantissa, checked(value._exponent + shift), true);
    }

    /// <summary>Arithmetic (flooring) right shift, as <see cref="Big"/>'s.</summary>
    public static ExactInteger operator >>(ExactInteger value, int shift)
    {
        if (shift < 0) return value << -shift;
        if (value.IsZero) return default;
        if (shift <= value._exponent) return new ExactInteger(value._mantissa, value._exponent - shift, true);
        return Make(value._mantissa >> (shift - value._exponent), 0);
    }

    public static ExactInteger operator &(ExactInteger left, ExactInteger right) => left.ToBig() & right.ToBig();
    public static ExactInteger operator |(ExactInteger left, ExactInteger right) => left.ToBig() | right.ToBig();
    public static ExactInteger operator ^(ExactInteger left, ExactInteger right) => left.ToBig() ^ right.ToBig();
    public static ExactInteger operator ~(ExactInteger value) => ~value.ToBig();

    // ---- comparison ----
    public int CompareTo(ExactInteger other)
    {
        var sign = Sign;
        if (sign != other.Sign) return sign < other.Sign ? -1 : 1;
        if (sign == 0) return 0;
        if (_exponent == other._exponent) return _mantissa.CompareTo(other._mantissa);
        var magnitude = BitLength(Big.Abs(_mantissa)) + _exponent;
        var otherMagnitude = BitLength(Big.Abs(other._mantissa)) + other._exponent;
        if (magnitude != otherMagnitude) return (magnitude < otherMagnitude ? -1 : 1) * sign;
        return _exponent < other._exponent
            ? _mantissa.CompareTo(other._mantissa << (other._exponent - _exponent))
            : (_mantissa << (_exponent - other._exponent)).CompareTo(other._mantissa);
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ExactInteger other => CompareTo(other),
        Big other => CompareTo(new ExactInteger(other)),
        _ => throw new ArgumentException("Object must be an integer.", nameof(obj)),
    };

    public bool Equals(ExactInteger other) => _exponent == other._exponent && _mantissa.Equals(other._mantissa);
    public override bool Equals(object? obj) => obj is ExactInteger other && Equals(other);
    public override int GetHashCode() => ToBig().GetHashCode();

    public static bool operator ==(ExactInteger left, ExactInteger right) => left.Equals(right);
    public static bool operator !=(ExactInteger left, ExactInteger right) => !left.Equals(right);
    public static bool operator <(ExactInteger left, ExactInteger right) => left.CompareTo(right) < 0;
    public static bool operator >(ExactInteger left, ExactInteger right) => left.CompareTo(right) > 0;
    public static bool operator <=(ExactInteger left, ExactInteger right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ExactInteger left, ExactInteger right) => left.CompareTo(right) >= 0;

    // ---- Big's static surface ----
    public static ExactInteger Abs(ExactInteger value) => value.Sign < 0 ? -value : value;
    public static ExactInteger Negate(ExactInteger value) => -value;
    public static ExactInteger Max(ExactInteger left, ExactInteger right) => left.CompareTo(right) < 0 ? right : left;
    public static ExactInteger Min(ExactInteger left, ExactInteger right) => left.CompareTo(right) <= 0 ? left : right;
    public static int Compare(ExactInteger left, ExactInteger right) => left.CompareTo(right);
    public static ExactInteger Add(ExactInteger left, ExactInteger right) => left + right;
    public static ExactInteger Subtract(ExactInteger left, ExactInteger right) => left - right;
    public static ExactInteger Multiply(ExactInteger left, ExactInteger right) => left * right;
    public static ExactInteger Divide(ExactInteger dividend, ExactInteger divisor) => dividend / divisor;
    public static ExactInteger Remainder(ExactInteger dividend, ExactInteger divisor) => dividend % divisor;

    public static ExactInteger DivRem(ExactInteger dividend, ExactInteger divisor, out ExactInteger remainder)
    {
        var quotient = dividend / divisor;
        remainder = dividend - quotient * divisor;
        return quotient;
    }

    public static ExactInteger GreatestCommonDivisor(ExactInteger left, ExactInteger right)
    {
        if (left.IsZero) return Abs(right);
        if (right.IsZero) return Abs(left);
        // Odd mantissas: the power of two in the GCD is the smaller exponent.
        return new ExactInteger(Big.GreatestCommonDivisor(left._mantissa, right._mantissa), Math.Min(left._exponent, right._exponent), true);
    }

    public static ExactInteger Pow(ExactInteger value, int exponent)
    {
        if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        if (exponent == 0) return One;
        if (value.IsZero) return default;
        return new ExactInteger(Big.Pow(value._mantissa, exponent), checked(value._exponent * exponent), true);
    }

    public static ExactInteger ModPow(ExactInteger value, ExactInteger exponent, ExactInteger modulus) =>
        Big.ModPow(value.ToBig(), exponent.ToBig(), modulus.ToBig());

    public static double Log(ExactInteger value) => Big.Log(value.ToBig());
    public static double Log(ExactInteger value, double baseValue) => Big.Log(value.ToBig(), baseValue);
    public static double Log10(ExactInteger value) => Big.Log10(value.ToBig());

    public static ExactInteger Parse(string value) => Big.Parse(value);
    public static ExactInteger Parse(string value, IFormatProvider? provider) => Big.Parse(value, provider);
    public static ExactInteger Parse(string value, NumberStyles style, IFormatProvider? provider) => Big.Parse(value, style, provider);

#if NET8_0_OR_GREATER
    public static ExactInteger TrailingZeroCount(ExactInteger value) =>
        value.IsZero ? Big.TrailingZeroCount(Big.Zero) : (ExactInteger)value._exponent;
#endif

    /// <summary>As <see cref="Big"/>'s <c>GetBitLength</c>: the bits of the shortest two's complement form without
    /// its sign bit.</summary>
    public long GetBitLength()
    {
        if (Sign >= 0) return IsZero ? 0 : BitLength(_mantissa) + _exponent;
        // -(m·2^e) - 1 = |m|·2^e - 1, which has one bit fewer than |m|·2^e only when that is a power of two.
        var bits = BitLength(Big.Abs(_mantissa)) + _exponent;
        return _mantissa == Big.MinusOne ? bits - 1 : bits;
    }

    public byte[] ToByteArray() => ToBig().ToByteArray();

    public override string ToString() => ToBig().ToString();
    public string ToString(string? format) => ToBig().ToString(format);
    public string ToString(IFormatProvider? provider) => ToBig().ToString(provider);
    public string ToString(string? format, IFormatProvider? formatProvider) => ToBig().ToString(format, formatProvider);

    // ---- helpers ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long BitLength(Big nonNegative)
    {
#if NET8_0_OR_GREATER
        return nonNegative.GetBitLength();
#else
        return FrameworkCompatibility.GetBitLengthPortable(nonNegative);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TrailingZeros(long nonZero)
    {
#if NETFRAMEWORK
        var zeros = 0;
        while ((nonZero & 1) == 0) { nonZero >>= 1; zeros++; }
        return zeros;
#else
        return System.Numerics.BitOperations.TrailingZeroCount(nonZero);
#endif
    }

    private static int TrailingZeros(Big nonZero)
    {
#if NET8_0_OR_GREATER
        return (int)Big.TrailingZeroCount(nonZero);
#else
        var bytes = Big.Abs(nonZero).ToByteArray();
        var zeros = 0;
        var index = 0;
        while (bytes[index] == 0) { zeros += 8; index++; }
        for (var b = bytes[index]; (b & 1) == 0; b >>= 1) zeros++;
        return zeros;
#endif
    }
}
