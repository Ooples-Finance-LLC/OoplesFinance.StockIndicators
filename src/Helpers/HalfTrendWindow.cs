using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HalfTrendWindow : IDisposable
{
    private readonly int _length;
    private readonly Average? _atr, _highMean, _lowMean;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count;
    private bool _seeded, _falling, _seekFall;
    private double _maxLow, _minHigh, _up, _down, _previousHigh, _previousLow, _previousClose;
    internal HalfTrendWindow(MovingAvgType kind, int length, int atrLength, bool external = false)
    { _length = Math.Max(1, length); if (!external) { _atr = new(kind, Math.Max(1, atrLength)); _highMean = new(kind, _length); _lowMean = new(kind, _length); } }
    private static RocBankValue Range(double high, double low, double previous)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous);
        var units = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p))); var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum);
    }
    private static int Compare(RocBankValue value, double price)
    { var difference = new ExactMeanAccumulator(); value.AddTo(ref difference); difference.Add(price, -1); return difference.Sign; }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool commit)
    {
        var first = deque.First; var threshold = _count - _length + 1L;
        while (first is not null && first.Value.Index < threshold) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (commit)
        {
            while (deque.First is { } old && old.Value.Index < threshold) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal (double Value, Signal Signal) Next(double high, double low, double close, bool commit, double? externalAtr = null, double? externalHigh = null, double? externalLow = null)
    {
        var highest = Extreme(_highs, high, true, commit); var lowest = Extreme(_lows, low, false, commit);
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _atr!.Next(Range(high, low, _seeded ? _previousClose : close), commit);
        var highMean = externalHigh.HasValue ? new RocBankValue(externalHigh.Value) : _highMean!.Next(new(high), commit);
        var lowMean = externalLow.HasValue ? new RocBankValue(externalLow.Value) : _lowMean!.Next(new(low), commit);
        var maxLow = _seeded ? _maxLow : lowest; var minHigh = _seeded ? _minHigh : highest;
        var priorUp = _seeded ? _up : lowest; var priorDown = _seeded ? _down : highest; var falling = _falling; var seekFall = _seekFall;
        if (_seekFall)
        {
            maxLow = Math.Max(lowest, maxLow);
            if (Compare(highMean, maxLow) < 0 && close < _previousLow) { falling = true; seekFall = false; minHigh = highest; }
        }
        else
        {
            minHigh = Math.Min(highest, minHigh);
            if (Compare(lowMean, minHigh) > 0 && close > (_seeded ? _previousHigh : highest)) { falling = false; seekFall = true; maxLow = lowest; }
        }
        var up = falling ? 0 : _falling ? priorDown : Math.Max(maxLow, priorUp);
        var down = falling ? !_falling ? priorUp : Math.Min(minHigh, priorDown) : 0;
        var halfAtr = atr.Multiply(.5); var arrow = new ExactMeanAccumulator(); arrow.Add(falling ? down : up); halfAtr.AddTo(ref arrow, falling ? 1 : -1);
        var signal = falling != _falling && !arrow.IsExactlyZero ? falling ? Signal.Sell : Signal.Buy : Signal.None;
        if (commit) { _count++; _seeded = true; _falling = falling; _seekFall = seekFall; _maxLow = maxLow; _minHigh = minHigh; _up = up; _down = down; _previousHigh = high; _previousLow = low; _previousClose = close; }
        return (falling ? down : up, signal);
    }
    internal void Reset() { _atr?.Reset(); _highMean?.Reset(); _lowMean?.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _seeded = _falling = _seekFall = false; _maxLow = _minHigh = _up = _down = _previousHigh = _previousLow = _previousClose = 0; }
    public void Dispose() { _atr?.Dispose(); _highMean?.Dispose(); _lowMean?.Dispose(); }
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
