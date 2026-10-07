using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Population skewness, with zero startup/constant-window conventions.
internal sealed class ExactSkewnessWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum, _squares, _cubes;
    internal ExactSkewnessWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));
    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var length = _window.Capacity;
        var expired = _window.Count == length ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var cubes = _cubes + current * current * current - expired * expired * expired;
        double result = 0;
        if (_window.Count >= length - 1)
        {
            var n = new BigInteger(length);
            var centered = n * squares - sum * sum;
            var third = n * n * cubes - 3 * n * sum * squares + 2 * sum * sum * sum;
            if (!centered.IsZero && !third.IsZero)
                result = third.Sign * ExactPopulationDeviation.RootRatio((third * third) << 2148, centered * centered * centered);
        }
        if (commit)
        {
            _window.TryAdd(value, out _);
            _sum = sum; _squares = squares; _cubes = cubes;
        }
        return result;
    }
    internal void Reset() { _window.Clear(); _sum = default; _squares = default; _cubes = default; }
    public void Dispose() => _window.Dispose();
}
