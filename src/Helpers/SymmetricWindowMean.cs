namespace OoplesFinance.StockIndicators.Helpers;

// Integer triangular weights with zero padding before the window fills.
internal sealed class SymmetricWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly long _weight;

    internal SymmetricWindowMean(int length)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _weight = TotalWeight(_values.Capacity);
    }

    internal static long TotalWeight(int length) => ((long)length + 1) * ((long)length + 1) / 4;

    internal double Next(double value, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _values.Capacity && lag <= _values.Count; lag++)
            sum.Add(lag == 0 ? value : _values[_values.Count - lag], Math.Min(lag + 1, _values.Capacity - lag));
        var result = sum.Mean(_weight);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
