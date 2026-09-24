using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Squared correlation with equally spaced time, evaluated from exact centered moments.
internal sealed class ExactRSquaredWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum, _squares, _weighted;
    internal ExactRSquaredWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var length = _window.Capacity;
        var full = _window.Count == length;
        var expired = full ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var weighted = full ? _weighted - _sum + expired + (length - 1) * current : _weighted + _window.Count * current;
        double result = 0;
        if (_window.Count >= length - 1 && length > 1)
        {
            var n = new BigInteger(length);
            var centered = n * squares - sum * sum;
            var covariance = 2 * weighted - (n - 1) * sum;
            if (!centered.IsZero)
                result = ExactMeanAccumulator.UnitRatio((3 * covariance * covariance) << 1074, (n * n - 1) * centered);
        }
        if (commit)
        {
            _window.TryAdd(value, out _);
            _sum = sum; _squares = squares; _weighted = weighted;
        }
        return result;
    }
    internal void Reset() { _window.Clear(); _sum = default; _squares = default; _weighted = default; }
    public void Dispose() => _window.Dispose();
}
