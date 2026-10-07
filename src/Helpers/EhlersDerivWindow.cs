using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EhlersDerivWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly Average _signal;
    private RocBankValue _last, _older, _oldest;
    internal EhlersDerivWindow(MovingAvgType kind, int length, int signal)
    { _length = Math.Max(1, length); _signal = new(kind, Math.Max(1, signal)); }
    private static RocBankValue Add(RocBankValue first, RocBankValue second)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum); return RocBankValue.Round(sum); }
    internal RocBankValue Line(double price, bool commit)
    {
        var change = new ExactMeanAccumulator(); if (_prices.Count == _length) { change.Add(price); change.Add(_prices.Peek(), -1); }
        var difference = RocBankValue.Round(change); var line = Add(Add(Add(difference, _last), _older), _oldest);
        if (commit) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); _oldest = _older; _older = _last; _last = difference; }
        return line;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var line = Line(price, commit); var signal = _signal.Next(line, commit); return (line.Publish(), signal.Publish()); }
    internal void Reset() { _prices.Clear(); _last = _older = _oldest = default; _signal.Reset(); }
    public void Dispose() => _signal.Dispose();
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
