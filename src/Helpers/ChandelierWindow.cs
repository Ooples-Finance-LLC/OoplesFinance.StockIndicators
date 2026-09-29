using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChandelierWindow : IDisposable
{
    private readonly int _length;
    private readonly double _multiplier;
    private readonly Average? _range;
    private readonly Queue<(double High, double Low)> _history = new();
    private ExactMeanAccumulator _bullish, _bearish;
    private double _previous; private bool _seeded;
    internal ChandelierWindow(MovingAvgType kind, int length, double multiplier, bool external = false)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        _length = Math.Max(1, length); _multiplier = multiplier;
        if (!external) _range = new(kind, _length);
    }
    internal (double Long, double Short, Signal Signal) Next(double high, double low, double close, bool commit, double? externalAtr = null)
    {
        var highest = high; var lowest = low; var skip = _history.Count == _length;
        foreach (var retained in _history) { if (skip) { skip = false; continue; } highest = Math.Max(highest, retained.High); lowest = Math.Min(lowest, retained.Low); }
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _range!.Next(TrueRange(high, low, _seeded ? _previous : close), commit);
        var distance = new ExactMeanAccumulator(); distance.AddProduct(atr.Mantissa, _multiplier); distance.ScaleByPowerOfTwo(atr.UpperShift);
        var longSum = new ExactMeanAccumulator(); longSum.Add(highest); longSum.Subtract(distance);
        var shortSum = distance; shortSum.Add(lowest);
        var longStop = RocBankValue.Round(longSum); var shortStop = RocBankValue.Round(shortSum);
        var bullish = new ExactMeanAccumulator(); bullish.Add(close); longStop.AddTo(ref bullish, -1);
        var bearish = new ExactMeanAccumulator(); bearish.Add(close); shortStop.AddTo(ref bearish, -1);
        var bullChange = bullish; bullChange.Subtract(_bullish); var bearChange = bearish; bearChange.Subtract(_bearish);
        var signal = bullish.Sign > 0 && bullChange.Sign > 0 ? Signal.StrongBuy : bearish.Sign < 0 && bearChange.Sign < 0 ? Signal.StrongSell : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((high, low)); _bullish = bullish; _bearish = bearish; _previous = close; _seeded = true; }
        return (longStop.Publish(), shortStop.Publish(), signal);
    }
    internal void Reset() { _history.Clear(); _range?.Reset(); _bullish = _bearish = default; _previous = 0; _seeded = false; }
    public void Dispose() { _history.Clear(); _range?.Dispose(); }
    private static RocBankValue TrueRange(double high, double low, double previous)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous);
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p)));
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, range); return RocBankValue.Round(sum);
    }
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
