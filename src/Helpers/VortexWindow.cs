using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VortexWindow : IDisposable
{
    private readonly PooledRingBuffer<(BigInteger Plus, BigInteger Minus, BigInteger Range)> _window;
    private BigInteger _plus, _minus, _range, _previousHigh, _previousLow, _previousClose;
    private bool _hasPrevious;
    internal VortexWindow(int length) => _window = new(Math.Max(1, length));
    internal (double Plus, double Minus) Next(double high, double low, double close, bool commit)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close);
        var previousClose = _hasPrevious ? _previousClose : c;
        var plusChange = _hasPrevious ? BigInteger.Abs(h - _previousLow) : BigInteger.Zero;
        var minusChange = _hasPrevious ? BigInteger.Abs(l - _previousHigh) : BigInteger.Zero;
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previousClose), BigInteger.Abs(l - previousClose)));
        var expired = _window.Count == _window.Capacity ? _window[0] : default;
        var plus = _plus + plusChange - expired.Plus; var minus = _minus + minusChange - expired.Minus;
        var totalRange = _range + range - expired.Range;
        var result = totalRange.IsZero ? (0d, 0d) :
            (ExactMeanAccumulator.UnitRatio(plus << 1074, totalRange), ExactMeanAccumulator.UnitRatio(minus << 1074, totalRange));
        if (commit)
        {
            _window.TryAdd((plusChange, minusChange, range), out _); _plus = plus; _minus = minus; _range = totalRange;
            _previousHigh = h; _previousLow = l; _previousClose = c; _hasPrevious = true;
        }
        return result;
    }
    internal void Reset() { _window.Clear(); _plus = _minus = _range = _previousHigh = _previousLow = _previousClose = 0; _hasPrevious = false; }
    public void Dispose() => _window.Dispose();
}
