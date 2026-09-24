using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Population residual RMS (regression=true), or population standard error of the mean.
internal sealed class ExactStandardErrorWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly bool _regression;
    private BigInteger _sum, _squares, _weighted;

    internal ExactStandardErrorWindow(int length, bool regression)
    {
        _window = new PooledRingBuffer<double>(Math.Max(1, length));
        _regression = regression;
    }

    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var length = _window.Capacity;
        var expired = _window.Count == length ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var weighted = !_regression ? BigInteger.Zero : _window.Count == length
            ? _weighted - _sum + expired + (length - 1) * current
            : _weighted + _window.Count * current;
        double result = 0;
        if (_window.Count >= length - 1)
        {
            var n = new BigInteger(length);
            var centered = n * squares - sum * sum;
            if (!_regression)
                result = ExactPopulationDeviation.RootRatio(centered, n * n * n);
            else if (length > 1)
            {
                // Remove the fitted slope's exact contribution from centered squares.
                var spread = n * n - 1;
                var covariance = 2 * weighted - (length - 1) * sum;
                result = ExactPopulationDeviation.RootRatio(spread * centered - 3 * covariance * covariance, n * n * spread);
            }
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
