using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TripleDelayWindow : IDisposable
{
    private readonly RocBankValue[] _first = new RocBankValue[6], _second = new RocBankValue[12];
    private readonly Average _line, _signal;
    private int _index;
    internal static bool Supports(MovingAvgType kind) => kind == MovingAvgType.EhlersModifiedOptimumEllipticFilter || StrengthWindow.Supports(kind);
    internal TripleDelayWindow(MovingAvgType kind, int length) { length = Math.Max(1, length); _line = new(kind, length); _signal = new(kind, length); }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal RocBankValue Detrend(double price, bool commit)
    {
        var olderFirst = _first[_index % 6]; var olderSecond = _second[(_index + 6) % 12]; var oldestSecond = _second[_index];
        var first = Add(new RocBankValue(price), olderFirst.Multiply(.088));
        var second = Add(Add(Add(first, olderFirst, -1), olderSecond.Multiply(1.2)), oldestSecond.Multiply(.7), -1);
        var raw = Add(Add(oldestSecond, olderSecond.Multiply(2), -1), second);
        if (commit) { _first[_index % 6] = first; _second[_index] = second; _index = (_index + 1) % 12; }
        return raw;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var line = _line.Next(Detrend(price, commit), commit); var signal = _signal.Next(line, commit); return (line.Publish(), signal.Publish()); }
    internal void Reset() { Array.Clear(_first, 0, _first.Length); Array.Clear(_second, 0, _second.Length); _index = 0; _line.Reset(); _signal.Reset(); }
    public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    private sealed class ExtendedElliptic
    {
        private RocBankValue _price1, _price2, _price3, _value1, _value2;
        private int _count;
        private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(negative); }
        private static RocBankValue Lead(RocBankValue value, RocBankValue previous) => Add(value.Multiply(2), previous, -1);
        internal RocBankValue Next(RocBankValue price, bool commit)
        {
            var first = _count < 1 ? price : _price1; var second = _count < 2 ? first : _price2; var third = _count < 3 ? second : _price3;
            var previous = _count < 1 ? price : _value1; var older = _count < 2 ? previous : _value2;
            var sum = new ExactMeanAccumulator(); Product(ref sum, Lead(price, first), .13785); Product(ref sum, Lead(first, second), .0007);
            Product(ref sum, Lead(second, third), .13785); Product(ref sum, previous, 1.2103); Product(ref sum, older, -.4867);
            var result = RocBankValue.Round(sum);
            if (commit) { _price3 = second; _price2 = first; _price1 = price; _value2 = previous; _value1 = result; if (_count < 3) _count++; }
            return result;
        }
        internal void Reset() { _price1 = _price2 = _price3 = _value1 = _value2 = default; _count = 0; }
    }
    private sealed class Average : IDisposable
    {
        private readonly ExtendedElliptic? _elliptic;
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind == MovingAvgType.EhlersModifiedOptimumEllipticFilter) _elliptic = new(); else if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_elliptic is not null) return _elliptic.Next(value, commit);
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _elliptic?.Reset(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
