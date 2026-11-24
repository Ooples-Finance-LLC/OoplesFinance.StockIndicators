namespace OoplesFinance.StockIndicators.Helpers;

// Integer triangular weights with zero padding before the window fills.
internal sealed class SymmetricWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly long _weight;
    private readonly int _length, _shortLength, _longLength;
    private ExactMeanAccumulator _sum, _shortRecent, _longRecent, _weighted;

    internal SymmetricWindowMean(int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length);
        _shortLength = (_length - 1) / 2;
        _longLength = _length / 2;
        _values = new PooledRingBuffer<double>(Math.Min(_length, Math.Max(1, capacityHint)));
        _weight = TotalWeight(_length);
    }

    internal static long TotalWeight(int length) => ((long)length + 1) * ((long)length + 1) / 4;

    internal double Next(double value, bool commit)
    {
        // Advancing the triangular taps adds the new price and two recent
        // half-windows, then subtracts the complete prior window. For even
        // periods the unequal half lengths preserve the two-tap plateau.
        var weighted = _weighted;
        weighted.Add(value);
        weighted.AddExact(_shortRecent);
        weighted.AddExact(_longRecent);
        weighted.Subtract(_sum);
        var result = weighted.Mean(_weight);
        if (commit)
        {
            if (_values.Count == _length) _sum.Add(_values[0], -1);
            AdvanceRecent(ref _shortRecent, _shortLength, value);
            AdvanceRecent(ref _longRecent, _longLength, value);
            _sum.Add(value);
            _weighted = weighted;
            _values.TryAdd(value, out _);
        }
        return result;
    }

    private void AdvanceRecent(ref ExactMeanAccumulator recent, int length, double value)
    {
        if (length == 0) return;
        if (_values.Count >= length) recent.Add(_values[_values.Count - length], -1);
        recent.Add(value);
    }

    internal void Reset() { _values.Clear(); _sum = _shortRecent = _longRecent = _weighted = default; }
    public void Dispose() => _values.Dispose();
}
