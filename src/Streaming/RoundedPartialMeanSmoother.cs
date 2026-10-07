namespace OoplesFinance.StockIndicators.Streaming;

// Impulse signals average the available samples, including their startup prefix.
internal sealed class RoundedPartialMeanSmoother : IMovingAverageSmoother
{
    private readonly PooledRingBuffer<double> _window;
    private ExactMeanAccumulator _sum;
    private bool _invalid;

    internal RoundedPartialMeanSmoother(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    public double Next(double value, bool isFinal)
    {
        var invalid = _invalid || double.IsNaN(value) || double.IsInfinity(value);
        if (isFinal) _invalid = invalid;
        if (invalid) return double.NaN;
        var next = _sum;
        var count = Math.Min(_window.Count + 1, _window.Capacity);
        if (_window.Count == _window.Capacity) next.Add(_window[0], -1);
        next.Add(value);
        if (isFinal) { _sum = next; _window.TryAdd(value, out _); }
        return next.Mean(count);
    }

    public void Reset() { _sum = default; _window.Clear(); _invalid = false; }
    public void Dispose() => _window.Dispose();
}
