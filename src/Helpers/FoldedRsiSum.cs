namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class FoldedRsiSum : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private ExactMeanAccumulator _sum;
    internal FoldedRsiSum(int length) => _window = new(Math.Max(1, length));
    internal double Next(double rsi, bool final)
    {
        var folded = 2 * Math.Abs(rsi - 50);
        var sum = _sum;
        if (_window.Count == _window.Capacity) sum.Add(_window[0], -1);
        sum.Add(folded);
        if (final) { _sum = sum; _window.TryAdd(folded, out _); }
        return sum.Mean(1);
    }
    internal void Reset() { _sum = default; _window.Clear(); }
    public void Dispose() => _window.Dispose();
}
