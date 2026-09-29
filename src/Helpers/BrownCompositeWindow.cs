using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// RSI stays bounded after extended price differences. Keep only observed lag and
// moving-average history, even when a public period is Int32.MaxValue.
internal sealed class BrownCompositeWindow : IDisposable
{
    private readonly int _lag;
    private readonly PriceRsiWindow _first, _second;
    private readonly Average _level, _fast, _slow;
    private readonly Queue<double> _history = new();
    private BigInteger _bull, _bear;
    internal BrownCompositeWindow(MovingAvgType kind, int fast, int slow, int length, int lag, int smooth)
    {
        _lag = Math.Max(1, lag); smooth = Math.Max(1, smooth);
        _first = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, length));
        _second = new(MovingAvgType.WildersSmoothingMethod, smooth);
        _level = new(kind, smooth); _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow));
    }
    internal (double Line, double Fast, double Slow, Signal Signal) Next(double price, bool final,
        double? externalLevel = null, double? externalFast = null, double? externalSlow = null)
    {
        var first = _first.Next(price, final); var second = _second.Next(price, final);
        var level = externalLevel ?? _level.Next(second, final);
        // The formula rounds the lagged difference before adding the level.
        var momentum = _history.Count == _lag ? first - _history.Peek() : 0;
        var line = momentum + level;
        var fast = externalFast ?? _fast.Next(line, final); var slow = externalSlow ?? _slow.Next(line, final);
        var bull = ExactVarianceWindow.Units(line) - ExactVarianceWindow.Units(Math.Max(fast, slow));
        var bear = ExactVarianceWindow.Units(line) - ExactVarianceWindow.Units(Math.Min(fast, slow));
        var signal = bull.Sign > 0 && bull > _bull ? Signal.StrongBuy : bear.Sign < 0 && bear < _bear ? Signal.StrongSell
            : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { if (_history.Count == _lag) _history.Dequeue(); _history.Enqueue(first); _bull = bull; _bear = bear; }
        return (line, fast, slow, signal);
    }
    internal void Reset() { _first.Reset(); _second.Reset(); _level.Reset(); _fast.Reset(); _slow.Reset(); _history.Clear(); _bull = _bear = default; }
    public void Dispose() { _first.Dispose(); _second.Dispose(); _level.Dispose(); _fast.Dispose(); _slow.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, _length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal double Next(double value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(new(value), final).Publish();
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); weighted.Add(value, _length);
            if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? 0 : sum.Mean(_length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
