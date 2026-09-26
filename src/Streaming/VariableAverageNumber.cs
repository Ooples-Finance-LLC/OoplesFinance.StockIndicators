namespace OoplesFinance.StockIndicators.Streaming;

// Two-component arithmetic is confined to VMA's directional index. Dividing a nearly settled
// index's window differences in binary64 alone amplifies rounding into large adaptive steps.
internal readonly struct VariableAverageNumber : IComparable<VariableAverageNumber>
{
    private readonly double _high;
    private readonly double _low;
    private VariableAverageNumber(double high, double low) { _high = high; _low = low; }
    public static implicit operator VariableAverageNumber(double value) => new(value, 0);
    public double Value => _high + _low;
    public int CompareTo(VariableAverageNumber other) => _high != other._high ? _high.CompareTo(other._high) : _low.CompareTo(other._low); // NOSONAR: S1244 - Lexicographic ordering must compare low parts only when high parts are equal.
    public static VariableAverageNumber Abs(VariableAverageNumber value) => value.CompareTo(0) < 0 ? -value : value;
    public static VariableAverageNumber operator -(VariableAverageNumber value) => new(-value._high, -value._low);
    public static VariableAverageNumber operator +(VariableAverageNumber a, VariableAverageNumber b)
    {
        var sum = a._high + b._high;
        var virtualB = sum - a._high;
        var error = (a._high - (sum - virtualB)) + (b._high - virtualB) + a._low + b._low;
        var high = sum + error;
        return new(high, error - (high - sum));
    }
    public static VariableAverageNumber operator -(VariableAverageNumber a, VariableAverageNumber b) => a + -b;
    public static VariableAverageNumber operator *(VariableAverageNumber a, VariableAverageNumber b)
    {
        var product = a._high * b._high;
#if NET7_0_OR_GREATER
        var error = Math.FusedMultiplyAdd(a._high, b._high, -product);
#else
        var error = ProductResidual(a._high, b._high, product);
#endif
        error += a._high * b._low + a._low * b._high + a._low * b._low;
        var high = product + error;
        return new(high, error - (high - product));
    }
    internal static double ProductResidual(double left, double right, double product)
    {
        const long mask = ~((1L << 27) - 1);
        var ah = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(left) & mask);
        var bh = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(right) & mask);
        var al = left - ah; var bl = right - bh;
        return ((ah * bh - product) + ah * bl + al * bh) + al * bl;
    }
    public static VariableAverageNumber operator /(VariableAverageNumber a, VariableAverageNumber b)
    {
        VariableAverageNumber quotient = a._high / b._high;
        var remainder = a - b * quotient;
        quotient += (remainder._high + remainder._low) / b._high;
        remainder = a - b * quotient;
        return quotient + (remainder._high + remainder._low) / b._high;
    }
}

// Fixed-storage monotone deque: committed and preview ranks both remain O(1) amortized per bar.
internal sealed class VariableAverageExtrema
{
    private readonly VariableAverageNumber[] _values;
    private readonly long[] _indices;
    private readonly bool _maximum;
    private int _start, _count;
    internal VariableAverageExtrema(int length, bool maximum)
    { _values = new VariableAverageNumber[length]; _indices = new long[length]; _maximum = maximum; }
    private bool Better(VariableAverageNumber a, VariableAverageNumber b) => _maximum ? a.CompareTo(b) >= 0 : a.CompareTo(b) <= 0;
    internal VariableAverageNumber Next(VariableAverageNumber value, long index, bool commit)
    {
        if (!commit)
        {
            var skip = _count > 0 && _indices[_start] <= index - _values.Length ? 1 : 0;
            if (_count == skip) return value;
            var first = _values[(_start + skip) % _values.Length];
            return Better(value, first) ? value : first;
        }
        if (_count > 0 && _indices[_start] <= index - _values.Length) { _start = (_start + 1) % _values.Length; _count--; }
        while (_count > 0 && Better(value, _values[(_start + _count - 1) % _values.Length])) _count--;
        var tail = (_start + _count) % _values.Length;
        _values[tail] = value; _indices[tail] = index; _count++;
        return _values[_start];
    }
    internal void Reset() { _start = _count = 0; }
}
