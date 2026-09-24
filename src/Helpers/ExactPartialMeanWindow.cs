namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ExactPartialMeanWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private ExactMeanAccumulator _sum;
    internal ExactPartialMeanWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));
    internal double Next(double value, bool commit)
    {
        var sum = _sum;
        if (_window.Count == _window.Capacity) sum.Add(_window[0], -1);
        sum.Add(value);
        var result = sum.Mean(Math.Min(_window.Count + 1, _window.Capacity));
        if (commit) { _sum = sum; _window.TryAdd(value, out _); }
        return result;
    }
    internal void Reset() { _sum = default; _window.Clear(); }
    public void Dispose() => _window.Dispose();
}
