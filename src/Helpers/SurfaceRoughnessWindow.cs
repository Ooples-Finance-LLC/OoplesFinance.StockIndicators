using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
// Partial-window Pearson correlation of price and its one-bar lag, seeded with zero.
internal sealed class SurfaceRoughnessWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<(double X, double Y)> _history = new();
    private readonly Average _price, _signal;
    private BigInteger _x, _y, _xx, _yy, _xy;
    private double _previous;
    internal SurfaceRoughnessWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _price = new(kind, _length); _signal = new(kind, _length); }
    internal double Line(double value, bool commit)
    {
        var x = ExactVarianceWindow.Units(_previous); var y = ExactVarianceWindow.Units(value);
        var full = _history.Count == _length; var oldest = full ? _history.Peek() : default;
        var expiredX = ExactVarianceWindow.Units(oldest.X); var expiredY = ExactVarianceWindow.Units(oldest.Y);
        var sx = _x + x - expiredX; var sy = _y + y - expiredY;
        var sxx = _xx + x * x - expiredX * expiredX; var syy = _yy + y * y - expiredY * expiredY; var sxy = _xy + x * y - expiredX * expiredY;
        var n = new BigInteger(full ? _history.Count : _history.Count + 1); var covariance = n * sxy - sx * sy;
        var xSpread = n * sxx - sx * sx; var ySpread = n * syy - sy * sy;
        var correlation = xSpread.IsZero || ySpread.IsZero ? 0 : covariance.Sign * ExactPopulationDeviation.RootRatio((covariance * covariance) << 2148, xSpread * ySpread);
        var result = 1 - ((correlation + 1) / 2);
        if (commit)
        {
            _x = sx; _y = sy; _xx = sxx; _yy = syy; _xy = sxy;
            if (full) _history.Dequeue(); _history.Enqueue((_previous, value)); _previous = value;
        }
        return result;
    }
    internal (double Line, double Average, double Signal) Next(double value, bool commit)
    { var line = Line(value, commit); return (line, _price.Next(new RocBankValue(value), commit).Publish(), _signal.Next(new RocBankValue(line), commit).Publish()); }
    internal void Reset() { _history.Clear(); _previous = 0; _x = _y = _xx = _yy = _xy = default; _price.Reset(); _signal.Reset(); }
    public void Dispose() { _price.Dispose(); _signal.Dispose(); }
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
