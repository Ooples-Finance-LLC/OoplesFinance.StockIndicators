using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FourierPhaseWindow : IDisposable
{
    private readonly int _length;
    private readonly List<double> _history = new();
    private readonly Average? _average;
    internal FourierPhaseWindow(int length, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, bool rawOnly = false)
    { _length = Math.Max(2, length); if (!rawOnly) _average = new(kind, _length); }
    internal (double Phase, double Signal) Next(double price, bool commit)
    {
        var baseline = _history.Count + 1L >= _length ? price : 0;
        var real = new ExactMeanAccumulator(); var imaginary = new ExactMeanAccumulator(); var mass = new ExactMeanAccumulator();
        for (var lag = 0; lag < _length && lag <= _history.Count; lag++)
        {
            var value = lag == 0 ? price : _history[_history.Count - lag]; var angle = 2 * Math.PI * lag / _length; var cosine = Math.Cos(angle); var sine = Math.Sin(angle);
            real.AddProduct(value, cosine); real.AddProduct(baseline, -cosine); imaginary.AddProduct(value, sine); imaginary.AddProduct(baseline, -sine);
            if (value >= baseline) { mass.Add(value); mass.Add(baseline, -1); } else { mass.Add(baseline); mass.Add(value, -1); }
        }
        var x = real.Ratio(mass); var y = imaginary.Ratio(mass); var negligible = Math.Abs(x) + Math.Abs(y) <= 1e-12;
        var phase = negligible ? 90 : Math.Atan2(y, x) * (180 / Math.PI) + 90;
        if (phase < 0) phase += 360; if (phase >= 360) phase -= 360; if (phase < 1e-10 || phase > 360 - 1e-10) phase = 0;
        var signal = _average?.Next(new RocBankValue(phase), commit).Publish() ?? 0;
        if (commit) { if (_history.Count == _length) _history.RemoveAt(0); _history.Add(price); } return (phase, signal);
    }
    internal void Reset() { _history.Clear(); _average?.Reset(); }
    public void Dispose() { _history.Clear(); _average?.Dispose(); }
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
