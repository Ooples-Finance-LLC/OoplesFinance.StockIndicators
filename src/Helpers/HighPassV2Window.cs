using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HighPassV2Window : IDisposable
{
    private readonly double _c1, _c2, _c3;
    private readonly Average _first, _second;
    private double _price1, _price2;
    private RocBankValue _high1, _high2;
    private long _count;
    internal HighPassV2Window(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        var angle = MathHelper.Sqrt2 * Math.PI / length;
        var decay = MathHelper.Exp(-angle);
        _c2 = 2 * decay * Math.Cos(angle); _c3 = -decay * decay; _c1 = (1 + _c2 - _c3) / 4;
        _first = new(kind, length); _second = new(kind, length);
    }
    private static RocBankValue Add(RocBankValue left, RocBankValue right, int sign = 1)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal RocBankValue Raw(double price, bool commit)
    {
        var difference = Add(Add(new RocBankValue(price), new RocBankValue(_price1).Multiply(2), -1), new RocBankValue(_price2));
        var high = _count < 4 ? default : Add(Add(difference.Multiply(_c1), _high1.Multiply(_c2)), _high2.Multiply(_c3));
        if (commit) { _price2 = _price1; _price1 = price; _high2 = _high1; _high1 = high; _count++; }
        return high;
    }
    internal RocBankValue Next(double price, bool commit)
    { var first = _first.Next(Raw(price, commit), commit); return _second.Next(first, commit); }
    internal void Reset() { _price1 = _price2 = 0; _high1 = _high2 = default; _count = 0; _first.Reset(); _second.Reset(); }
    public void Dispose() { _first.Dispose(); _second.Dispose(); }
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
