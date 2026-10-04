using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Retains rational intermediate values through every smoothing stage.
// MacZWindow.Average has a different contract: it rounds recursive state.
internal sealed class UnroundedMovingAverage : IDisposable
{
    private readonly MovingAvgType _kind; private readonly int _length;
    private readonly Queue<Number> _history = new();
    private readonly IMovingAverageSmoother? _fallback;
    private Number _sum, _weighted, _previous; private long _count;
    internal UnroundedMovingAverage(MovingAvgType kind, int length)
    { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
    internal Number Next(Number value, bool final)
    {
        if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
        if (_length == 1) return value;
        var sum = _sum; var weighted = _weighted; Number result;
        var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
        if (window)
        {
            weighted = weighted - sum + value.Times(_length);
            if (_history.Count == _length) sum -= _history.Peek(); sum += value;
            result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                : _count + 1 < _length ? default : sum.Divide(_length);
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        { sum += value; result = sum.Divide(_count + 1); }
        else
        { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length); }
        if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
        return result;
    }
    internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
    public void Dispose() => _fallback?.Dispose();
}
