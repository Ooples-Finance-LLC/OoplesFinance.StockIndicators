namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SequentialMeanGate : IDisposable
{
    private readonly PooledRingBuffer<int> _directions;
    private long _sum;
    private double _previousMean, _previousOutput;
    private bool _hasPrevious;

    internal SequentialMeanGate(int length) => _directions = new PooledRingBuffer<int>(Math.Max(1, length));

    internal double Next(double price, double mean, bool commit)
    {
        var direction = mean.CompareTo(_hasPrevious ? _previousMean : 0);
        var full = _directions.Count == _directions.Capacity;
        var sum = _sum + direction - (full ? _directions[0] : 0);
        var ready = _directions.Count >= _directions.Capacity - 1;
        var result = ready && Math.Abs(sum) == _directions.Capacity
            ? mean : _hasPrevious ? _previousOutput : price;
        if (commit)
        {
            _directions.TryAdd(direction, out _);
            _sum = sum;
            _previousMean = mean;
            _previousOutput = result;
            _hasPrevious = true;
        }
        return result;
    }

    internal void Reset()
    {
        _directions.Clear();
        _sum = 0;
        _previousMean = _previousOutput = 0;
        _hasPrevious = false;
    }
    public void Dispose() => _directions.Dispose();
}
