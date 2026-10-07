namespace OoplesFinance.StockIndicators.Streaming;

// Each stage of a composed average rounds its exact window once. A merely
// bounded first-stage error can become a large relative error after cancellation.
internal sealed class RoundedSimpleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly PooledRingBuffer<double> _window;
    private ExactMeanAccumulator _sum;

    internal RoundedSimpleMovingAverageSmoother(int length)
        => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    public double Next(double value, bool isFinal)
    {
        var next = _sum;
        if (_window.Count == _window.Capacity) next.Add(_window[0], -1);
        next.Add(value);
        var ready = _window.Count >= _window.Capacity - 1;
        if (isFinal) { _sum = next; _window.TryAdd(value, out _); }
        return ready ? next.Mean(_window.Capacity) : 0;
    }

    public void Reset() { _sum = default; _window.Clear(); }
    public void Dispose() => _window.Dispose();
}
