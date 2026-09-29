using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SuperTrendWindow : IDisposable
{
    private readonly double _factor;
    private readonly Average? _range;
    private RocBankValue _long, _short, _trend;
    private double _previous;
    private bool _seeded, _up = true;
    internal SuperTrendWindow(MovingAvgType kind, int length, double factor, bool external = false)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _factor = factor; if (!external) _range = new(kind, Math.Max(1, length));
    }
    internal (double Value, Signal Signal) Next(double high, double low, double close, bool commit, double? externalAtr = null)
    {
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _range!.Next(TrueRange(high, low, _seeded ? _previous : close), commit);
        RocBankValue Candidate(int sign) { var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, _factor, sign); sum.ScaleByPowerOfTwo(atr.UpperShift); sum.Add(close); return RocBankValue.Round(sum); }
        int Compare(double price, RocBankValue stop) { var sum = new ExactMeanAccumulator(); sum.Add(price); stop.AddTo(ref sum, -1); return sum.Sign; }
        RocBankValue Extreme(RocBankValue candidate, RocBankValue prior, bool maximum) { var sum = new ExactMeanAccumulator(); candidate.AddTo(ref sum); prior.AddTo(ref sum, -1); return maximum ? sum.Sign > 0 ? candidate : prior : sum.Sign < 0 ? candidate : prior; }
        var lower = Candidate(-1); var upper = Candidate(1); var priorLower = _seeded ? _long : lower; var priorUpper = _seeded ? _short : upper; var previous = _seeded ? _previous : 0;
        var longStop = Compare(previous, priorLower) > 0 ? Extreme(lower, priorLower, true) : lower;
        var shortStop = Compare(previous, priorUpper) < 0 ? Extreme(upper, priorUpper, false) : upper;
        var up = _up ? Compare(close, priorLower) >= 0 : Compare(close, priorUpper) > 0; var trend = up ? longStop : shortStop;
        var difference = new ExactMeanAccumulator(); difference.Add(close); trend.AddTo(ref difference, -1); var change = difference; change.Add(previous, -1); _trend.AddTo(ref change);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _long = longStop; _short = shortStop; _trend = trend; _previous = close; _up = up; _seeded = true; }
        return (trend.Publish(), signal);
    }
    internal void Reset() { _range?.Reset(); _long = _short = _trend = default; _previous = 0; _seeded = false; _up = true; }
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
