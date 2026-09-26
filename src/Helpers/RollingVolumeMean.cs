namespace OoplesFinance.StockIndicators.Helpers;

// Exact window eviction, including products whose binary64 value would overflow.
internal sealed class RollingVolumeMean : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices, _volumes;
    private ExactVolumeMean _mean;

    internal RollingVolumeMean(int length)
    {
        _length = Math.Max(1, length);
        _prices = new PooledRingBuffer<double>(_length);
        _volumes = new PooledRingBuffer<double>(_length);
    }

    internal double Next(double price, double volume, bool commit)
    {
        var next = _mean;
        if (_prices.Count == _length) next.Add(_prices[0], _volumes[0], -1);
        next.Add(price, volume);
        var ready = _prices.Count >= _length - 1;
        if (commit)
        {
            _prices.TryAdd(price, out _); _volumes.TryAdd(volume, out _);
            _mean = next;
        }
        return ready ? next.Value() : 0;
    }

    internal void Reset() { _mean = default; _prices.Clear(); _volumes.Clear(); }
    public void Dispose() { _prices.Dispose(); _volumes.Dispose(); }
}
