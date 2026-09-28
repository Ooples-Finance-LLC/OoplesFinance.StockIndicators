using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RepulseWindow : IDisposable
{
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private readonly Average _bull, _bear, _signal;
    private double _previousOpen;
    private long _count;
    internal static int PowerPeriod(int length) => (int)Math.Min(int.MaxValue, Math.Max(1L, length) * 5);
    internal RepulseWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _bull = new(kind, PowerPeriod(_length)); _bear = new(kind, PowerPeriod(_length)); _signal = new(kind, _length); }
    private double Extreme(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        var node = values.First; while (node is not null && node.Value.Index <= _count - _length) node = node.Next;
        return node is null ? value : maximum ? Math.Max(value, node.Value.Value) : Math.Min(value, node.Value.Value);
    }
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Percent(RocBankValue numerator, double close)
    { if (close == 0) return default; var sum = new ExactMeanAccumulator(); numerator.Multiply(100).AddTo(ref sum); return RocBankValue.Round(sum, close); }
    internal (RocBankValue Bull, RocBankValue Bear) Powers(double open, double high, double low, double close, bool commit)
    {
        var highest = Extreme(_high, high, true); var lowest = Extreme(_low, low, false);
        var triple = new RocBankValue(close).Multiply(3); var previous = new RocBankValue(_previousOpen);
        var bull = Percent(Add(Add(triple, new RocBankValue(lowest).Multiply(2), -1), previous, -1), close);
        var bear = Percent(Add(Add(previous, new RocBankValue(highest).Multiply(2)), triple, -1), close);
        if (commit) { Commit(_high, high, true); Commit(_low, low, false); _previousOpen = open; _count++; }
        return (bull, bear);
    }
    internal (double Line, double Signal) Next(double open, double high, double low, double close, bool commit)
    {
        var power = Powers(open, high, low, close, commit); var bull = _bull.Next(power.Bull, commit); var bear = _bear.Next(power.Bear, commit);
        var line = Add(bull, bear, -1); var signal = _signal.Next(line, commit); return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _previousOpen = 0; _count = 0; _bull.Reset(); _bear.Reset(); _signal.Reset(); }
    public void Dispose() { _bull.Dispose(); _bear.Dispose(); _signal.Dispose(); }
    // Grow simple/weighted history as observations arrive, including for saturated periods.
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
