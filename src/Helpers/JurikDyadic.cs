using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact dyadic arithmetic for the Jurik recurrence. On modern runtimes ordinary
// weighted differences fit 127 magnitude bits. No rounded floating-point proxy
// is used to decide whether a fast result is correct. Larger exponent spans fall
// back to the established arbitrary-precision accumulator.
internal struct JurikDyadic
{
    private ExactMeanAccumulator _fallback;
#if !NETFRAMEWORK
    private Int128 _integer;
    private int _exponent;
    private bool _wide;
    internal int Sign => _wide ? _fallback.Sign : _integer.CompareTo(0);
#else
    internal int Sign => _fallback.Sign;
#endif

    internal void Add(double value)
    {
#if !NETFRAMEWORK
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArithmeticException("A dyadic input must be finite.");
        var mantissa = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
        var other = new JurikDyadic { _integer = bits < 0 ? -mantissa : mantissa,
            _exponent = Math.Max(0, exponent - 1) - 1074 };
        other.Normalize(); AddExact(other);
#else
        _fallback.Add(value);
#endif
    }
    internal void ScaleByPowerOfTwo(int exponent)
    {
#if !NETFRAMEWORK
        if (!_wide) { _exponent = checked(_exponent + exponent); return; }
#endif
        _fallback.ScaleByPowerOfTwo(exponent);
    }
    internal void Subtract(JurikDyadic other)
    {
#if !NETFRAMEWORK
        if (!_wide && !other._wide) { other._integer = -other._integer; AddExact(other); return; }
#endif
        var sum = Accumulator(); sum.Subtract(other.Accumulator()); SetFallback(sum);
    }
    internal void AddExact(JurikDyadic other)
    {
#if !NETFRAMEWORK
        if (!_wide && !other._wide)
        {
            if (other._integer == 0) return;
            if (_integer == 0) { this = other; return; }
            var common = Math.Min(_exponent, other._exponent);
            if (Shift(_integer, _exponent - common, out var left)
                && Shift(other._integer, other._exponent - common, out var right)
                && !(right > 0 && left > Int128.MaxValue - right)
                && !(right < 0 && left < -Int128.MaxValue - right))
            {
                _integer = left + right; _exponent = common; Normalize(); return;
            }
        }
#endif
        var sum = Accumulator(); sum.AddExact(other.Accumulator()); SetFallback(sum);
    }
    internal void Multiply(double factor)
    {
#if !NETFRAMEWORK
        var other = new JurikDyadic(); other.Add(factor);
        if (!_wide)
        {
            if (_integer == 0 || other._integer == 0) { this = default; return; }
            if (Magnitude(_integer) <= (UInt128)Int128.MaxValue / Magnitude(other._integer))
            {
                _integer *= other._integer; _exponent += other._exponent; Normalize(); return;
            }
        }
#endif
        var product = Accumulator(); product.Multiply(factor); SetFallback(product);
    }
    internal double Mean(int count)
    {
#if !NETFRAMEWORK
        if (!_wide && count == 1) return RoundedDouble();
#endif
        return Accumulator().Mean(count);
    }
    internal double Ratio(JurikDyadic denominator)
    {
#if !NETFRAMEWORK
        if (!_wide && !denominator._wide && TryRatio(_integer, _exponent,
            denominator._integer, denominator._exponent, out var result)) return result;
#endif
        return Accumulator().Ratio(denominator.Accumulator());
    }
    internal JurikDyadic Rounded()
    {
        var value = Mean(1);
        var result = new JurikDyadic();
        if (!double.IsInfinity(value)) { result.Add(value); return result; }
        var wide = RocBankValue.Round(Accumulator());
        result.Add(wide.Mantissa); result.ScaleByPowerOfTwo(wide.UpperShift);
        return result;
    }
    internal static JurikDyadic SumProduct(JurikDyadic sum, JurikDyadic term, double factor)
    {
#if !NETFRAMEWORK
        if (sum.TryDouble(out var addend) && term.TryDouble(out var operand))
        {
            var value = Math.FusedMultiplyAdd(operand, factor, addend);
            if (!double.IsInfinity(value))
            {
                var result = new JurikDyadic(); result.Add(value); return result;
            }
        }
#endif
        term.Multiply(factor); term.AddExact(sum); return term.Rounded();
    }
    internal static bool TryBlend(JurikDyadic old, JurikDyadic next, double weight, out JurikDyadic result)
    {
        result = default;
#if !NETFRAMEWORK
        if (old.TryDouble(out var a) && next.TryDouble(out var b))
        {
            var difference = b - a;
            var virtualA = difference - b;
            var error = (b - (difference - virtualA)) + (-a - virtualA);
            if (double.IsFinite(difference) && error == 0) // NOSONAR: exact TwoSum certificate, not tolerance.
            {
                var value = Math.FusedMultiplyAdd(difference, weight, a);
                if (double.IsFinite(value)) { result.Add(value); return true; }
            }
        }
#endif
        return false;
    }
    private void SetFallback(ExactMeanAccumulator value)
    {
        this = default; _fallback = value;
#if !NETFRAMEWORK
        _wide = true;
#endif
    }
    private ExactMeanAccumulator Accumulator()
    {
#if !NETFRAMEWORK
        if (!_wide)
        {
            var sum = new ExactMeanAccumulator();
            var magnitude = Magnitude(_integer);
            var sign = _integer < 0 ? -1 : 1;
            for (var shift = 0; magnitude != 0; shift += 32, magnitude >>= 32)
            {
                var part = new ExactMeanAccumulator();
                part.Add((double)(uint)(magnitude & uint.MaxValue), sign);
                part.ScaleByPowerOfTwo(_exponent + shift); sum.AddExact(part);
            }
            return sum;
        }
#endif
        return _fallback;
    }
#if !NETFRAMEWORK
    private bool TryDouble(out double value)
    {
        value = 0;
        if (_wide || _exponent < -1074) return false;
        var bits = Bits(Magnitude(_integer));
        if (bits > 53 || _exponent + bits - 1 > 1023) return false;
        value = RoundedDouble(); return true;
    }
    private double RoundedDouble()
    {
        if (_integer == 0) return 0;
        var magnitude = Magnitude(_integer);
        var grid = Math.Max(_exponent + Bits(magnitude) - 53, -1074);
        var shift = grid - _exponent;
        UInt128 rounded;
        if (shift > 127) rounded = 0;
        else if (shift > 0)
        {
            rounded = magnitude >> shift;
            var half = (UInt128)1 << (shift - 1);
            var remainder = magnitude & ((half << 1) - 1);
            if (remainder > half || remainder == half && (rounded & 1) != 0) rounded++;
        }
        else rounded = magnitude << -shift;
        var mantissa = (ulong)rounded;
        if (mantissa >= 1UL << 53) { mantissa >>= 1; grid++; }
        if (grid > 971) return _integer < 0 ? double.NegativeInfinity : double.PositiveInfinity;
        var payload = grid > -1074 || mantissa >= 1UL << 52
            ? ((ulong)(grid + 1075) << 52) | (mantissa - (1UL << 52)) : mantissa;
        if (_integer < 0) payload |= 1UL << 63;
        return BitConverter.Int64BitsToDouble(unchecked((long)payload));
    }
    private static UInt128 Magnitude(Int128 value) => (UInt128)(value < 0 ? -value : value);
    private void Normalize()
    {
        if (_integer == 0) { _exponent = 0; return; }
        var magnitude = Magnitude(_integer);
        var lower = (ulong)(magnitude & ulong.MaxValue);
        var zeros = lower != 0 ? BitOperations.TrailingZeroCount(lower)
            : 64 + BitOperations.TrailingZeroCount((ulong)(magnitude >> 64));
        _integer >>= zeros; _exponent += zeros;
    }
    private static int Bits(UInt128 value)
    {
        var upper = (ulong)(value >> 64);
        return upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)value)
            : 128 - BitOperations.LeadingZeroCount(upper);
    }
    private static bool Shift(Int128 value, int shift, out Int128 result)
    {
        if (shift >= 127 || Bits(Magnitude(value)) + shift > 127) { result = 0; return false; }
        result = value << shift; return true;
    }
    private static bool TryRatio(Int128 numerator, int numeratorExponent, Int128 denominator,
        int denominatorExponent, out double result)
    {
        result = 0;
        if (numerator == 0 || denominator == 0) return true;
        var negative = (numerator < 0) != (denominator < 0);
        var top = Magnitude(numerator); var bottom = Magnitude(denominator);
        var power = Bits(top) - Bits(bottom);
        if (power >= 0 ? top < (bottom << power) : (top << -power) < bottom) power--;
        var scale = numeratorExponent - denominatorExponent;
        var exponent = power + scale;
        if (exponent > 1023) { result = negative ? double.NegativeInfinity : double.PositiveInfinity; return true; }
        if (exponent < -1075) { result = negative ? -0d : 0d; return true; }
        var grid = Math.Max(exponent - 52, -1074);
        var shift = scale - grid;
        if (shift >= 0)
        {
            if (shift >= 128 || Bits(top) + shift > 128) return false;
            top <<= shift;
        }
        else
        {
            if (-shift >= 128 || Bits(bottom) - shift > 128) return false;
            bottom <<= -shift;
        }
        var quotient = top / bottom; var remainder = top % bottom;
        var otherHalf = bottom - remainder;
        if (remainder > otherHalf || remainder == otherHalf && (quotient & 1) != 0) quotient++;
        result = Math.ScaleB((double)quotient, grid);
        if (negative) result = -result;
        return true;
    }
#endif
}
