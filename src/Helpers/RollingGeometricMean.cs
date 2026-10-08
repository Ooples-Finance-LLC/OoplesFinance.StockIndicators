using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RollingGeometricMean : IDisposable
{
    private const double Floor = 0.000001;
    private readonly PooledRingBuffer<double> _values;
    private readonly bool _positiveOnly;
    private BigInteger _product = BigInteger.One;
    private long _power;
    private int _used;

    internal RollingGeometricMean(int length, bool positiveOnly = false)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _positiveOnly = positiveOnly;
    }

    private static (long Mantissa, int Power) Decode(double value)
    {
        if (!(value > 0) || double.IsInfinity(value)) throw new ArithmeticException("A geometric product requires positive finite factors.");
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var mantissa = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
        var power = exponent == 0 ? -1074 : exponent - 1075;
        while ((mantissa & 1) == 0) { mantissa >>= 1; power++; }
        return (mantissa, power);
    }

    private static int BitLength(BigInteger value)
    {
        var bytes = value.ToByteArray();
        var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var bits = last * 8;
        for (var head = bytes[last]; head != 0; head >>= 1) bits++;
        return bits;
    }

    private static BigInteger IntegerRoot(BigInteger value, int degree)
    {
        if (degree == 1 || value.IsZero) return value;
        if (degree == 2) return ExactPopulationDeviation.IntegerRoot(value);
        var current = BigInteger.One << ((BitLength(value) - 1) / degree + 1);
        while (true)
        {
            var next = ((degree - 1) * current + value / BigInteger.Pow(current, degree - 1)) / degree;
            if (next >= current) return current;
            current = next;
        }
    }

    private static double RoundRoot(BigInteger product, long power, int degree)
    {
        var exponent = BitLength(product) - 1L + power;
        var rootExponent = exponent >= 0 ? exponent / degree : (exponent - degree + 1) / degree;
        var grid = (int)Math.Max(-1074, rootExponent - 52);
        var shift = checked((int)(power - (long)degree * grid));
        var whole = shift >= 0 ? product << shift : product >> -shift;
        var root = IntegerRoot(whole, degree);
        var midpoint = BigInteger.Pow(2 * root + 1, degree);
        var comparisonShift = checked(shift + degree);
        var comparison = comparisonShift >= 0 ? (product << comparisonShift).CompareTo(midpoint)
            : product.CompareTo(midpoint << -comparisonShift);
        if (comparison > 0 || comparison == 0 && !root.IsEven) root++;
        return ExactMeanAccumulator.Encode((ulong)root, grid + 1074, false);
    }

    internal double Next(double value, bool commit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        value = _positiveOnly ? value : Math.Max(value, Floor);
        var product = _product;
        var power = _power;
        var used = _used;
        if (_values.Count == _values.Capacity && _values[0] > 0)
        {
            var old = Decode(_values[0]);
            product /= old.Mantissa;
            power -= old.Power;
            used--;
        }
        if (value > 0)
        {
            var current = Decode(value);
            product *= current.Mantissa;
            power += current.Power;
            used++;
        }
        var result = _values.Count >= _values.Capacity - 1
            ? used > 0 ? RoundRoot(product, power, used) : 0
            : _positiveOnly ? value : 0;
        if (commit) { _product = product; _power = power; _used = used; _values.TryAdd(value, out _); }
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, bool positiveOnly = false)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        if (input.Length < length)
        {
            if (positiveOnly) input.CopyTo(output);
            else output.Slice(0, input.Length).Clear();
            return;
        }
        using var mean = new RollingGeometricMean(length, positiveOnly);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset() { _values.Clear(); _product = BigInteger.One; _power = 0; _used = 0; }
    public void Dispose() => _values.Dispose();
}
