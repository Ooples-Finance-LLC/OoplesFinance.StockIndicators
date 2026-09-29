using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class WilderVolatilityWindow : IDisposable
{
    private readonly int _extremaLength;
    private readonly double _factor;
    private readonly Average? _range, _trend;
    private readonly Queue<double> _prices = new();
    private ExactMeanAccumulator _difference;
    private double _previous; private bool _seeded;
    internal WilderVolatilityWindow(MovingAvgType kind, int trendLength, int rangeLength, double factor, bool external = false)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _extremaLength = Math.Max(2, rangeLength); _factor = factor;
        if (!external) { _range = new(kind, Math.Max(1, rangeLength)); _trend = new(kind, Math.Max(1, trendLength)); }
    }
    internal (double Value, Signal Signal) Next(double high, double low, double close, bool commit, double? externalAtr = null, double? externalTrend = null)
    {
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _range!.Next(TrueRange(high, low, _seeded ? _previous : close), commit);
        var trend = externalTrend.HasValue ? new RocBankValue(externalTrend.Value) : _trend!.Next(new RocBankValue(close), commit);
        var direction = new ExactMeanAccumulator(); direction.Add(close); trend.AddTo(ref direction, -1); var up = direction.Sign > 0;
        var highest = close; var lowest = close; var skip = _prices.Count == _extremaLength;
        foreach (var price in _prices) { if (skip) { skip = false; continue; } highest = Math.Max(highest, price); lowest = Math.Min(lowest, price); }
        var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, _factor, up ? -1 : 1); sum.ScaleByPowerOfTwo(atr.UpperShift); sum.Add(up ? highest : lowest); var stop = RocBankValue.Round(sum);
        var difference = new ExactMeanAccumulator(); difference.Add(close); stop.AddTo(ref difference, -1); var change = difference; change.Subtract(_difference);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_prices.Count == _extremaLength) _prices.Dequeue(); _prices.Enqueue(close); _previous = close; _seeded = true; _difference = difference; }
        return (stop.Publish(), signal);
    }
    internal void Reset() { _prices.Clear(); _range?.Reset(); _trend?.Reset(); _previous = 0; _seeded = false; _difference = default; }
    public void Dispose() { _prices.Clear(); _range?.Dispose(); _trend?.Dispose(); }
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
