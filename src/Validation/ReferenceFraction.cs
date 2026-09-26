using System.Numerics;

namespace OoplesFinance.StockIndicators.Validation;

// Validation-only exact rational arithmetic. Used where normalizing a tiny quantity makes
// a rounded-to-zero oracle unsafe. This code is not used by production calculations.
internal readonly struct ReferenceFraction : IComparable<ReferenceFraction>
{
    private readonly BigInteger _numerator, _denominator;
    internal ReferenceFraction(long value) : this(new BigInteger(value), BigInteger.One) { }
    internal ReferenceFraction(BigInteger value) : this(value, BigInteger.One) { }
    private ReferenceFraction(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        _numerator = numerator / divisor; _denominator = denominator / divisor;
    }
    // Validation-only binary64 precision with an unbounded upper exponent.
    // Select a normal-sized mantissa directly; ordinary and subnormal rounding
    // still use ToDouble. This avoids repeatedly reducing enormous rationals.
    internal ReferenceFraction RoundExtendedBinary64()
    {
        var published = ToDouble();
        if (!double.IsInfinity(published)) return FromDouble(published);
        var exponentBound = BitLength(BigInteger.Abs(_numerator)) - BitLength(_denominator);
        var shift = Math.Max(512, ((exponentBound - 1023 + 511) / 512) * 512);
        var reduced = new ReferenceFraction(_numerator, _denominator << shift).ToDouble();
        if (double.IsInfinity(reduced))
        {
            shift += 512;
            reduced = new ReferenceFraction(_numerator, _denominator << shift).ToDouble();
        }
        var rounded = FromDouble(reduced);
        return new ReferenceFraction(rounded._numerator << shift, rounded._denominator);
    }

    internal int Sign => _numerator.Sign;
    internal ReferenceFraction Abs() => new(BigInteger.Abs(_numerator), _denominator);
    internal static ReferenceFraction FromDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var significand = new BigInteger(bits & ((1L << 52) - 1));
        if (exponent != 0) significand += BigInteger.One << 52;
        if (bits < 0) significand = -significand;
        var power = exponent == 0 ? -1074 : exponent - 1075;
        return power >= 0 ? new(significand << power, BigInteger.One) : new(significand, BigInteger.One << -power);
    }
    internal double LogisticPercentToDouble()
    {
        var magnitude = BigInteger.Abs(_numerator);
        if (magnitude >= 400 * _denominator) return Sign < 0 ? 0 : 100;
        const int precision = 192;
        var scale = BigInteger.One << precision;
        var argument = magnitude * scale / (512 * _denominator);
        var exponential = scale; var term = scale;
        for (var n = 1; n <= 60; n++) { term = term * argument / (scale * n); exponential += term; }
        for (var n = 0; n < 10; n++) exponential = exponential * exponential / scale;
        return new ReferenceFraction(100 * (Sign < 0 ? scale : exponential), scale + exponential).ToDouble();
    }

    internal double TanhToDouble()
    {
        var magnitude = BigInteger.Abs(_numerator);
        // Below 2^-27, |x - tanh(x)| < |x|^3/3 is below half a binary64 ulp.
        if ((magnitude << 27) < _denominator) return ToDouble();
        if (magnitude >= 20 * _denominator) return Sign;
        // Independent 192-bit fixed-point exp(2|x|/16), followed by four squarings.
        // With |x|<20, 80 Taylor terms leave a remainder below 2^-280;
        // fixed-point truncation after squaring stays below 2^-120.
        const int precision = 192;
        var scale = BigInteger.One << precision;
        var argument = magnitude * scale / (8 * _denominator);
        var term = scale; var exponential = scale;
        for (var n = 1; n <= 80; n++)
        {
            term = term * argument / (scale * n);
            exponential += term;
        }
        for (var n = 0; n < 4; n++) exponential = exponential * exponential / scale;
        return new ReferenceFraction(Sign * (exponential - scale), exponential + scale).ToDouble();
    }

    // Independent fixed-point atanh series. Reduction gives z in [0, 1/3].
    // At 192 fractional bits and 80 terms, truncation/rounding error stays below
    // 2^-168 even after scaling ln(2) by any finite binary64 quotient exponent.
    internal double LogToDouble()
    {
        if (Sign <= 0) throw new ArgumentOutOfRangeException(nameof(Sign));
        const int precision = 192;
        var scale = BigInteger.One << precision;
        var exponent = BitLength(_numerator) - BitLength(_denominator);
        if (exponent >= 0 ? _numerator < (_denominator << exponent)
            : (_numerator << -exponent) < _denominator) exponent--;
        var numerator = exponent < 0 ? _numerator << -exponent : _numerator;
        var denominator = exponent > 0 ? _denominator << exponent : _denominator;
        BigInteger Series(BigInteger z)
        {
            var square = z * z / scale;
            var term = z;
            var sum = term;
            for (var j = 1; j < 80; j++)
            {
                term = term * square / scale;
                sum += term / (2 * j + 1);
            }
            return 2 * sum;
        }
        var logarithm = Series((numerator - denominator) * scale / (numerator + denominator));
        logarithm += exponent * Series(scale / 3);
        return new ReferenceFraction(logarithm, scale).ToDouble();
    }

    internal double ToDouble()
    {
        if (_numerator.IsZero) return 0;
        var magnitude = BigInteger.Abs(_numerator);
        var exponent = BitLength(magnitude) - BitLength(_denominator);
        if (exponent >= 0 ? magnitude < (_denominator << exponent)
            : (magnitude << -exponent) < _denominator) exponent--;
        if (exponent > 1023) return _numerator.Sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;

        // Quantize the exact rational to binary64's integer significand grid. Subnormal
        // values share the 2^-1074 grid. No floating division may discard a midpoint residual.
        var gridExponent = Math.Max(-1074, exponent - 52);
        var numerator = gridExponent < 0 ? magnitude << -gridExponent : magnitude;
        var denominator = gridExponent > 0 ? _denominator << gridExponent : _denominator;
        var significand = BigInteger.DivRem(numerator, denominator, out var remainder);
        var comparison = (remainder * 2).CompareTo(denominator);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        // The rounded significand is at most 2^53, hence conversion to double is exact.
        var powerOfTwo = gridExponent >= -1022
            ? BitConverter.Int64BitsToDouble((long)(gridExponent + 1023) << 52)
            : BitConverter.Int64BitsToDouble(1L << (gridExponent + 1074));
        var result = (double)significand * powerOfTwo;
        return _numerator.Sign < 0 ? -result : result;
    }
    // Independent oracle: bisect ordered binary64 encodings and compare rational
    // squares, then choose across the exact midpoint. No integer-root production code.
    internal double SqrtToDouble()
    {
        if (Sign < 0) throw new ArgumentOutOfRangeException(nameof(_numerator));
        if (Sign == 0) return 0;
        long low = 0, high = 0x7fefffffffffffff;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            var candidate = FromDouble(BitConverter.Int64BitsToDouble(middle));
            if ((candidate * candidate).CompareTo(this) <= 0) low = middle;
            else high = middle - 1;
        }
        var lower = FromDouble(BitConverter.Int64BitsToDouble(low));
        if ((lower * lower).CompareTo(this) == 0) return lower.ToDouble();
        var upper = low == 0x7fefffffffffffff
            ? new ReferenceFraction(BigInteger.One << 1024, BigInteger.One)
            : FromDouble(BitConverter.Int64BitsToDouble(low + 1));
        var midpoint = (lower + upper) / new ReferenceFraction(2);
        var comparison = CompareTo(midpoint * midpoint);
        return comparison < 0 || comparison == 0 && (low & 1) == 0
            ? BitConverter.Int64BitsToDouble(low) : BitConverter.Int64BitsToDouble(low + 1);
    }

    internal double PositiveRootToDouble(int degree)
    {
        if (degree <= 0 || Sign < 0) throw new ArgumentOutOfRangeException(nameof(degree));
        if (degree == 1 || Sign == 0) return ToDouble();
        if (degree == 2) return SqrtToDouble();
        var numerator = _numerator;
        var denominator = _denominator;
        int ComparePower(ReferenceFraction candidate) =>
            (BigInteger.Pow(candidate._numerator, degree) * denominator).CompareTo(
                numerator * BigInteger.Pow(candidate._denominator, degree));
        long low = 0, high = 0x7fefffffffffffff;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (ComparePower(FromDouble(BitConverter.Int64BitsToDouble(middle))) <= 0) low = middle;
            else high = middle - 1;
        }
        var lower = FromDouble(BitConverter.Int64BitsToDouble(low));
        if (ComparePower(lower) == 0) return lower.ToDouble();
        var upper = low == 0x7fefffffffffffff
            ? new ReferenceFraction(BigInteger.One << 1024, BigInteger.One)
            : FromDouble(BitConverter.Int64BitsToDouble(low + 1));
        var midpoint = (lower + upper) / new ReferenceFraction(2);
        var comparison = ComparePower(midpoint);
        return comparison > 0 || comparison == 0 && (low & 1) == 0
            ? BitConverter.Int64BitsToDouble(low) : BitConverter.Int64BitsToDouble(low + 1);
    }

    private static int BitLength(BigInteger value)
    {
        var bytes = value.ToByteArray();
        var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var bits = last * 8; var head = bytes[last];
        while (head != 0) { bits++; head >>= 1; }
        return bits;
    }
    public int CompareTo(ReferenceFraction other) => (_numerator * other._denominator).CompareTo(other._numerator * _denominator);
    public static ReferenceFraction operator +(ReferenceFraction a, ReferenceFraction b) => new(a._numerator * b._denominator + b._numerator * a._denominator, a._denominator * b._denominator);
    public static ReferenceFraction operator -(ReferenceFraction a, ReferenceFraction b) => new(a._numerator * b._denominator - b._numerator * a._denominator, a._denominator * b._denominator);
    public static ReferenceFraction operator *(ReferenceFraction a, ReferenceFraction b) => new(a._numerator * b._numerator, a._denominator * b._denominator);
    public static ReferenceFraction operator /(ReferenceFraction a, ReferenceFraction b) => new(a._numerator * b._denominator, a._denominator * b._numerator);
}
