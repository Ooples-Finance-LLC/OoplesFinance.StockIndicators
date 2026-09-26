using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact zero-padded OLS endpoint weights: [6*(N-lag)-2*(N+1)] / [N*(N+1)].
internal sealed class SimplifiedLeastSquaresWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly long _denominator;
    private readonly BigInteger _newWeight;
    private readonly BigInteger _expiredWeight;
    private ExactMeanAccumulator _sixSum;
    private ExactMeanAccumulator _numerator;

    internal SimplifiedLeastSquaresWindow(int length)
    {
        var period = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(period);
        _denominator = (long)period * (period + 1L);
        _newWeight = 4L * period - 2;
        _expiredWeight = 2L * period + 2;
    }

    internal double Next(double value, bool commit)
    {
        // Every retained tap loses six units of weight. Remove the expired tap's
        // would-be negative weight and add the newest tap, using exact integers.
        var next = _numerator;
        next.Subtract(_sixSum);
        next.Add(value, _newWeight);
        if (_window.Count == _window.Capacity) next.Add(_window[0], _expiredWeight);
        var result = next.Mean(_denominator);
        if (commit)
        {
            var sum = _sixSum;
            sum.Add(value, 6);
            if (_window.Count == _window.Capacity) sum.Add(_window[0], -6);
            _window.TryAdd(value, out _);
            _sixSum = sum;
            _numerator = next;
        }
        return result;
    }

    internal void Reset() { _window.Clear(); _sixSum = default; _numerator = default; }
    public void Dispose() => _window.Dispose();
}
