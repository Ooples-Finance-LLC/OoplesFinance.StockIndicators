using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// OLS endpoint +/- a multiple of the rounded full-window population deviation.
internal sealed class ExactRegressionChannelWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly ExactLinearFitWindow _regression;
    private readonly double _multiplier;
    private BigInteger _sum, _squares;
    internal ExactRegressionChannelWindow(int length, double multiplier)
    {
        _window = new PooledRingBuffer<double>(Math.Max(1, length));
        _regression = new ExactLinearFitWindow(length);
        _multiplier = multiplier;
    }
    internal (double Upper, double Middle, double Lower) Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var n = new BigInteger(_window.Capacity);
        var expired = _window.Count == _window.Capacity ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var deviation = _window.Count < _window.Capacity - 1 ? 0
            : ExactPopulationDeviation.RootRatio(n * squares - sum * sum, n * n);
        var fit = _regression.Next(value, commit);
        if (commit)
        {
            _window.TryAdd(value, out _);
            _sum = sum; _squares = squares;
        }
        return (fit.Offset(deviation, _multiplier), fit.Last, fit.Offset(deviation, -_multiplier));
    }
    internal void Reset() { _window.Clear(); _regression.Reset(); _sum = default; _squares = default; }
    public void Dispose() { _window.Dispose(); _regression.Dispose(); }
}
