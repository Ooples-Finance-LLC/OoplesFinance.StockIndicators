namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SmoothedReturnWindow : IDisposable
{
    private readonly int _lag;
    private readonly StrengthAverage _mean;
    private readonly PooledRingBuffer<double> _history;
    private long _count;
    internal SmoothedReturnWindow(MovingAvgType kind, int lag, int smoothing, int capacityHint = int.MaxValue)
    {
        _lag = Math.Max(1, lag);
        _mean = new StrengthAverage(kind, smoothing, capacityHint);
        _history = new PooledRingBuffer<double>(Math.Min(_lag, Math.Max(1, capacityHint)));
    }
    internal double Next(double price, bool final)
    {
        var mean = _mean.Next(new StrengthValue(price), final).Mantissa;
        var previous = _count < _lag ? 0 : _history[0];
        var value = previous == 0 ? 100 : RoundedPercentageChange.Of(mean, previous);
        if (final) { _history.TryAdd(mean, out _); _count++; }
        return value;
    }
    internal void Reset() { _mean.Reset(); _history.Clear(); _count = 0; }
    public void Dispose() { _mean.Dispose(); _history.Dispose(); }
}
