using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Correctly rounded full-window population deviation.
internal sealed class ExactPopulationWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum, _squares;
    internal ExactPopulationWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var length = _window.Capacity;
        var full = _window.Count == length;
        var expired = full ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var n = new BigInteger(length);
        var result = _window.Count < length - 1 ? 0 : ExactPopulationDeviation.RootRatio(n * squares - sum * sum, n * n);
        if (commit)
        {
            _window.TryAdd(value, out _);
            _sum = sum; _squares = squares;
        }
        return result;
    }
    internal void Reset() { _window.Clear(); _sum = default; _squares = default; }
    public void Dispose() => _window.Dispose();
}
