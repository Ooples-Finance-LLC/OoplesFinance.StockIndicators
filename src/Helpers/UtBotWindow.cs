using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UtBotWindow : IDisposable
{
    private readonly double _factor;
    private readonly Average? _range;
    private RocBankValue _stop;
    private double _previous, _position;
    private bool _seeded;
    internal UtBotWindow(MovingAvgType kind, int length, double factor, bool external = false)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _factor = factor; if (!external) _range = new(kind, Math.Max(1, length));
    }
    internal (double Stop, double Position, double Buy, double Sell, Signal Signal) Next(double high, double low, double close, bool commit, double? externalAtr = null)
    {
        var previous = _seeded ? _previous : close;
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _range!.Next(TrueRange(high, low, previous), commit);
        var currentSide = new ExactMeanAccumulator(); currentSide.Add(close); _stop.AddTo(ref currentSide, -1);
        var previousSide = new ExactMeanAccumulator(); previousSide.Add(previous); _stop.AddTo(ref previousSide, -1);
        var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, _factor, currentSide.Sign > 0 ? -1 : 1); sum.ScaleByPowerOfTwo(atr.UpperShift); sum.Add(close); var candidate = RocBankValue.Round(sum);
        var movement = new ExactMeanAccumulator(); candidate.AddTo(ref movement); _stop.AddTo(ref movement, -1);
        var stop = currentSide.Sign > 0 && previousSide.Sign > 0 ? movement.Sign > 0 ? candidate : _stop : currentSide.Sign < 0 && previousSide.Sign < 0 ? movement.Sign < 0 ? candidate : _stop : candidate;
        var position = previousSide.Sign < 0 && currentSide.Sign > 0 ? 1 : previousSide.Sign > 0 && currentSide.Sign < 0 ? -1 : _position;
        var difference = new ExactMeanAccumulator(); difference.Add(close); stop.AddTo(ref difference, -1); var change = difference; change.Subtract(previousSide);
        var buy = _seeded && previousSide.Sign <= 0 && difference.Sign > 0 ? 1d : 0d; var sell = _seeded && previousSide.Sign >= 0 && difference.Sign < 0 ? 1d : 0d;
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _stop = stop; _previous = close; _position = position; _seeded = true; }
        return (stop.Publish(), position, buy, sell, signal);
    }
    internal void Reset() { _range?.Reset(); _stop = default; _previous = _position = 0; _seeded = false; }
    public void Dispose() => _range?.Dispose();
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
