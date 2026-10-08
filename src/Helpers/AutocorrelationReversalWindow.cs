using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AutocorrelationReversalWindow : IDisposable
{
    private readonly int _length, _firstLag;
    private readonly RoofAutocorrelationWindow _correlation;
    private readonly List<int> _crossings = new();
    private readonly Average? _average;
    private double _previous;
    internal AutocorrelationReversalWindow(MovingAvgType kind, int length, int smoothing, int firstLag, bool externalAverage = false)
    { _length = Math.Max(1, length); _firstLag = Math.Max(1, firstLag); _correlation = new(_length, smoothing); if (!externalAverage) _average = new(kind, Math.Max(1, smoothing)); }
    internal (double Value, Signal Signal) Next(double price, bool commit, double? externalAverage = null)
    {
        var correlation = _correlation.Next(price, commit).Value; var crossing = (correlation > .5 && _previous < .5) || (correlation < .5 && _previous > .5) ? 1 : 0;
        var count = 0; for (var lag = _firstLag - 1; lag < _length && lag <= _crossings.Count; lag++) count += lag == 0 ? crossing : _crossings[_crossings.Count - lag];
        var reversal = count > _length / 2d ? 1d : 0d;
        var mean = externalAverage.HasValue ? new RocBankValue(externalAverage.Value) : _average!.Next(new RocBankValue(price), commit); var distance = new ExactMeanAccumulator(); distance.Add(price); mean.AddTo(ref distance, -1);
        var signal = reversal == 0 ? Signal.None : distance.Sign < 0 ? Signal.Buy : distance.Sign > 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_crossings.Count == _length) _crossings.RemoveAt(0); _crossings.Add(crossing); _previous = correlation; } return (reversal, signal);
    }
    internal void Reset() { _correlation.Reset(); _crossings.Clear(); _average?.Reset(); _previous = 0; }
    public void Dispose() { _average?.Dispose(); _correlation.Reset(); _crossings.Clear(); }
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
