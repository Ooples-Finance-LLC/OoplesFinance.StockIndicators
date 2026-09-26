using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// An exact integer times 2^(scale-1074). Ordinary price windows fit signed 64-bit
// integers on their shared binary grid; wider exponent spans use BigInteger.
internal struct ExactMeanAccumulator
{
    private long _small;
    private BigInteger _large;
    private int _scale;
    private bool _wide;
    private bool IsZero => _wide ? _large.IsZero : _small == 0;
    internal bool IsExactlyZero => IsZero;
    internal int Sign => _wide ? _large.Sign : Math.Sign(_small);

    internal void ScaleByPowerOfTwo(int exponent)
    {
        if (!IsZero) _scale = checked(_scale + exponent);
    }

    internal static bool SevereCancellation(double left, double right, double result) =>
        left != 0 && right != 0 && Math.Abs(result) <= 1e-4 * Math.Max(Math.Abs(left), Math.Abs(right));

    internal void Add(double value, int weight = 1)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        var exponent = (int)((bits >> 52) & 0x7ff);
        if (exponent == 0x7ff) throw new ArithmeticException("An exact finite mean requires finite inputs.");
        var magnitude = (long)(bits & 0xfffffffffffffUL);
        if (exponent != 0) magnitude += 1L << 52;
        if (magnitude == 0 || weight == 0) return;
        var signed = (bits >> 63) == 0 ? magnitude : -magnitude;
        var scale = Math.Max(0, exponent - 1);
        var absoluteWeight = Math.Abs((long)weight);
        if (magnitude <= long.MaxValue / absoluteWeight)
            AddSmall(signed * weight, scale);
        else AddLarge(new BigInteger(signed) * weight, scale);
    }

    internal void Subtract(ExactMeanAccumulator value)
    {
        if (value._wide) AddLarge(-value._large, value._scale);
        else if (value._small == long.MinValue) AddLarge(-new BigInteger(value._small), value._scale);
        else AddSmall(-value._small, value._scale);
    }

    internal void Add(double value, BigInteger weight)
    {
        if (weight >= int.MinValue && weight <= int.MaxValue)
        {
            Add(value, (int)weight);
            return;
        }
        var term = new ExactMeanAccumulator();
        term.Add(value);
        AddLarge(new BigInteger(term._small) * weight, term._scale);
    }

    internal void AddProduct(double left, double right, int weight = 1)
    {
        static (long Mantissa, int Scale) Decode(double value)
        {
            var bits = BitConverter.DoubleToInt64Bits(value);
            var exponent = (int)((bits >> 52) & 2047);
            if (exponent == 2047) throw new ArithmeticException("An exact finite product requires finite inputs.");
            var mantissa = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
            return (bits < 0 ? -mantissa : mantissa, Math.Max(0, exponent - 1));
        }
        var a = Decode(left); var b = Decode(right);
        if (a.Mantissa == 0 || b.Mantissa == 0 || weight == 0) return;
        // Removing powers of two is exact and lets ordinary prices times integral
        // volume stay in the allocation-free signed-integer representation.
        while ((a.Mantissa & 1) == 0) { a.Mantissa >>= 1; a.Scale++; }
        while ((b.Mantissa & 1) == 0) { b.Mantissa >>= 1; b.Scale++; }
        var scale = a.Scale + b.Scale - 1074;
        if (Math.Abs(a.Mantissa) <= long.MaxValue / Math.Abs(b.Mantissa))
        {
            var product = a.Mantissa * b.Mantissa;
            if (Math.Abs(product) <= long.MaxValue / Math.Abs((long)weight))
            {
                AddSmall(product * weight, scale);
                return;
            }
        }
        AddLarge(new BigInteger(a.Mantissa) * b.Mantissa * weight, scale);
    }

    // Same binary64 quantization as Mean, with an exact (possibly wide) divisor.
    // A zero total weight retains the indicators' explicit zero-volume convention.
    internal double Ratio(ExactMeanAccumulator denominator)
    {
        if (IsZero || denominator.IsZero) return 0;
        var scale = _scale - denominator._scale + 1074;
        if (!denominator._wide && denominator._small != long.MinValue)
        {
            var numerator = this;
            numerator._scale = scale;
            var value = numerator.Mean(Math.Abs(denominator._small));
            return denominator._small < 0 ? -value : value;
        }
        var signed = _wide ? _large : new BigInteger(_small);
        var divisor = denominator._wide ? denominator._large : new BigInteger(denominator._small);
        var negative = signed.Sign != divisor.Sign;
        var magnitude = BigInteger.Abs(signed);
        divisor = BigInteger.Abs(divisor);
        var exponent = BitLength(magnitude) - BitLength(divisor);
        if (exponent >= 0 ? magnitude < (divisor << exponent) : (magnitude << -exponent) < divisor) exponent--;
        var grid = Math.Max(0, exponent + scale - 52);
        var shift = grid - scale;
        if (shift > 0) divisor <<= shift;
        else magnitude <<= -shift;
        var mantissa = BigInteger.DivRem(magnitude, divisor, out var remainder);
        var halfway = (remainder << 1).CompareTo(divisor);
        if (halfway > 0 || halfway == 0 && !mantissa.IsEven) mantissa++;
        return Encode((ulong)mantissa, grid, negative);
    }

    internal static double UnitRatio(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        var top = new ExactMeanAccumulator(); top.AddLarge(numerator, 0);
        var bottom = new ExactMeanAccumulator(); bottom.AddLarge(denominator, 1074);
        return top.Ratio(bottom);
    }

    private void AddSmall(long value, int scale)
    {
        if (value == 0) return;
        if (IsZero) { _small = value; _scale = scale; _wide = false; _large = default; return; }
        if (!_wide)
        {
            var common = Math.Min(_scale, scale);
            if (ShiftFits(_small, _scale - common, out var left) && ShiftFits(value, scale - common, out var right)
                && !(right > 0 && left > long.MaxValue - right)
                && !(right < 0 && left < long.MinValue - right))
            {
                _small = left + right;
                _scale = common;
                return;
            }
        }
        AddLarge(new BigInteger(value), scale);
    }

    private static bool ShiftFits(long value, int shift, out long result)
    {
        if (shift >= 64) { result = 0; return value == 0; }
        result = unchecked(value << shift);
        return (result >> shift) == value;
    }

    private void AddLarge(BigInteger value, int scale)
    {
        if (value.IsZero) return;
        var common = IsZero ? scale : Math.Min(_scale, scale);
        var previous = _wide ? _large : new BigInteger(_small);
        var total = IsZero ? value : (previous << (_scale - common)) + (value << (scale - common));
        if (total.IsZero) { this = default; return; }
        // Discard only exact powers of two, so old tiny inputs do not permanently force
        // a wide representation after they leave a rolling window.
        var bytes = BigInteger.Abs(total).ToByteArray();
        var trailing = 0;
        var index = 0;
        while (bytes[index] == 0) { trailing += 8; index++; }
        var low = bytes[index];
        while ((low & 1) == 0) { trailing++; low >>= 1; }
        total >>= trailing;
        _scale = common + trailing;
        _wide = total < long.MinValue || total > long.MaxValue;
        if (_wide) { _large = total; _small = 0; }
        else { _small = (long)total; _large = default; }
    }

    internal double SqrtMean(long count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (IsZero) return 0;
        var numerator = _wide ? _large : new BigInteger(_small);
        if (numerator.Sign < 0) throw new ArithmeticException("A real square root requires a nonnegative mean.");
        var divisor = new BigInteger(count);
        var exponent = BitLength(numerator) - BitLength(divisor);
        if (exponent >= 0 ? numerator < (divisor << exponent) : (numerator << -exponent) < divisor) exponent--;
        exponent += _scale - 1074;
        var rootExponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
        var grid = Math.Max(-1074, rootExponent - 52);
        var shift = _scale - 1074 - 2 * grid;
        if (shift >= 0) numerator <<= shift;
        else divisor <<= -shift;
        var whole = numerator / divisor;
        var root = whole.IsZero ? BigInteger.Zero : ExactPopulationDeviation.IntegerRoot(whole);
        var midpoint = 2 * root + 1;
        var comparison = (4 * numerator).CompareTo(divisor * midpoint * midpoint);
        if (comparison > 0 || comparison == 0 && !root.IsEven) root++;
        return Encode((ulong)root, grid + 1074, false);
    }

    // Squared binary64 integers use units of 2^-2148 instead of 2^-1074.
    internal static double SquaredUnitMean(BigInteger numerator, long count)
        => new ExactMeanAccumulator { _wide = true, _large = numerator, _scale = -1074 }.Mean(count);

    internal double Mean(long count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (IsZero) return 0;
        if (!_wide && TrySmallMean((ulong)count, out var value)) return value;
        var signed = _wide ? _large : new BigInteger(_small);
        var magnitude = BigInteger.Abs(signed);
        var divisor = new BigInteger(count);
        var exponent = BitLength(magnitude) - BitLength(divisor);
        if (exponent >= 0 ? magnitude < (divisor << exponent) : (magnitude << -exponent) < divisor) exponent--;
        var grid = Math.Max(0, exponent + _scale - 52);
        var shift = grid - _scale;
        if (shift > 0) divisor <<= shift;
        else magnitude <<= -shift;
        var mantissa = BigInteger.DivRem(magnitude, divisor, out var remainder);
        var halfway = (remainder << 1).CompareTo(divisor);
        if (halfway > 0 || halfway == 0 && !mantissa.IsEven) mantissa++;
        return Encode((ulong)mantissa, grid, signed.Sign < 0);
    }

    private bool TrySmallMean(ulong divisor, out double value)
    {
        var magnitude = _small < 0 ? unchecked((ulong)(~_small)) + 1 : (ulong)_small;
        var bits = BitLength(magnitude);
        var exponent = bits - BitLength(divisor);
        if (exponent >= 0 ? magnitude < (divisor << exponent) : (magnitude << -exponent) < divisor) exponent--;
        var grid = Math.Max(0, exponent + _scale - 52);
        var shift = grid - _scale;
        if (shift < 0)
        {
            if (bits - shift > 64) { value = 0; return false; }
            magnitude <<= -shift;
        }
        else
        {
            if (BitLength(divisor) + shift > 64) { value = 0; return false; }
            divisor <<= shift;
        }
        var mantissa = magnitude / divisor;
        var remainder = magnitude % divisor;
        // Compare to half without overflowing a doubled remainder.
        var otherHalf = divisor - remainder;
        if (remainder > otherHalf || remainder == otherHalf && (mantissa & 1) != 0) mantissa++;
        value = Encode(mantissa, grid, _small < 0);
        return true;
    }

    internal static double Encode(ulong mantissa, int grid, bool negative)
    {
        if (mantissa == (1UL << 53)) { mantissa >>= 1; grid++; }
        if (grid > 2045) return negative ? double.NegativeInfinity : double.PositiveInfinity;
        var payload = mantissa >= (1UL << 52) ? ((ulong)(grid + 1) << 52) | (mantissa - (1UL << 52)) : mantissa;
        if (negative) payload |= 1UL << 63;
        return BitConverter.Int64BitsToDouble(unchecked((long)payload));
    }

    private static int BitLength(ulong value)
    {
        var bits = 0;
        if (value >= (1UL << 32)) { value >>= 32; bits += 32; }
        if (value >= (1UL << 16)) { value >>= 16; bits += 16; }
        if (value >= (1UL << 8)) { value >>= 8; bits += 8; }
        if (value >= (1UL << 4)) { value >>= 4; bits += 4; }
        if (value >= (1UL << 2)) { value >>= 2; bits += 2; }
        if (value >= 2) { value >>= 1; bits++; }
        return bits + (value == 0 ? 0 : 1);
    }

    private static int BitLength(BigInteger value)
    {
        var bytes = value.ToByteArray();
        var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        return last * 8 + BitLength(bytes[last]);
    }
}
