using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Partial-window arithmetic mean absolute deviation, with no rounded mean stage.
internal sealed class ExactMeanAbsoluteDeviationWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum;
    internal ExactMeanAbsoluteDeviationWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));
    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var full = _window.Count == _window.Capacity;
        var sum = _sum + current - (full ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero);
        var n = new BigInteger(Math.Min(_window.Count + 1, _window.Capacity));
        var deviations = BigInteger.Abs(n * current - sum);
        for (var j = full ? 1 : 0; j < _window.Count; j++)
            deviations += BigInteger.Abs(n * ExactVarianceWindow.Units(_window[j]) - sum);
        var result = ExactMeanAccumulator.UnitRatio(deviations, n * n);
        if (commit) { _sum = sum; _window.TryAdd(value, out _); }
        return result;
    }
    internal void Reset() { _sum = default; _window.Clear(); }
    public void Dispose() => _window.Dispose();
}
