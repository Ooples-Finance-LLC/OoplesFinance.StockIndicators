using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FibonacciRetraceWindow : IDisposable
{
    private readonly int _length; private readonly double _factor; private readonly Average? _mean;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index; private ExactMeanAccumulator _previousBullish, _previousBearish;
    internal FibonacciRetraceWindow(MovingAvgType kind, int meanLength, int rangeLength, double factor, bool includeMean = true)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _length = Math.Max(1, rangeLength); _factor = factor;
        if (includeMean) _mean = new(kind, Math.Max(1, meanLength));
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _length + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    private RocBankValue Interpolate(double from, double to)
    { var sum = new ExactMeanAccumulator(); sum.Add(from); sum.AddProduct(from, _factor, -1); sum.AddProduct(to, _factor); return RocBankValue.Round(sum); }
    internal (double Upper, double Lower, Signal Trade) Next(double high, double low, double price, bool final, double? externalMean = null)
    {
        var highest = Extreme(_highs, high, true, final); var lowest = Extreme(_lows, low, false, final);
        var upper = Interpolate(highest, lowest); var lower = Interpolate(lowest, highest);
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean?.Next(new(price), final) ?? default;
        var bullish = new ExactMeanAccumulator(); mean.AddTo(ref bullish); upper.AddTo(ref bullish, -1);
        var bearish = new ExactMeanAccumulator(); mean.AddTo(ref bearish); lower.AddTo(ref bearish, -1);
        var bullChange = bullish; bullChange.Subtract(_previousBullish); var bearChange = bearish; bearChange.Subtract(_previousBearish);
        var trade = bullish.Sign > 0 && bullChange.Sign > 0 ? Signal.StrongBuy : bearish.Sign < 0 && bearChange.Sign < 0 ? Signal.StrongSell
            : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _index++; _previousBullish = bullish; _previousBearish = bearish; }
        return (upper.Publish(), lower.Publish(), trade);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _mean?.Reset(); _index = 0; _previousBullish = _previousBearish = default; }
    public void Dispose() => _mean?.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
