using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class OptimizedTrendWindow : IDisposable
{
    private readonly double _percent;
    private readonly Average? _mean;
    private readonly LazyVidya? _vidya;
    private OptimizedTrendStops _stops;
    private ExactMeanAccumulator _difference;
    internal OptimizedTrendWindow(MovingAvgType kind, int length, double percent, bool external = false)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent)) throw new ArgumentOutOfRangeException(nameof(percent));
        _percent = percent; if (!external) { if (kind == MovingAvgType.VariableIndexDynamicAverage) _vidya = new(Math.Max(1, length)); else _mean = new(kind, Math.Max(1, length)); }
    }
    internal static bool Supports(MovingAvgType kind) => StrengthWindow.Supports(kind) || kind == MovingAvgType.VariableIndexDynamicAverage;
    internal (double Value, Signal Signal) Next(double value, bool commit, double? externalMean = null)
    {
        var average = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _vidya is not null ? new RocBankValue(_vidya.Next(value, commit)) : _mean!.Next(new(value), commit);
        var stops = _stops; var line = stops.Next(average, _percent); var difference = new ExactMeanAccumulator(); difference.Add(value); line.AddTo(ref difference, -1); var change = difference; change.Subtract(_difference);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _stops = stops; _difference = difference; } return (line.Publish(), signal);
    }
    internal void Reset() { _mean?.Reset(); _vidya?.Reset(); _stops = default; _difference = default; }
    public void Dispose() => _mean?.Dispose();
    private sealed class LazyVidya
    {
        private readonly int _length; private readonly double _alpha;
        private readonly Queue<(double From, double To)> _history = new();
        private ExactMeanAccumulator _numerator, _denominator;
        private double _previous, _mean; private bool _seeded;
        internal LazyVidya(int length) { _length = length; _alpha = 2d / (length + 1d); }
        private static void Add(ref ExactMeanAccumulator numerator, ref ExactMeanAccumulator denominator, double from, double to, int sign)
        { numerator.Add(to, 100 * sign); numerator.Add(from, -100 * sign); var direction = to >= from ? sign : -sign; denominator.Add(to, direction); denominator.Add(from, -direction); }
        internal double Next(double value, bool commit)
        {
            var from = _seeded ? _previous : value; var numerator = _numerator; var denominator = _denominator;
            if (_history.Count == _length) { var old = _history.Peek(); Add(ref numerator, ref denominator, old.From, old.To, -1); }
            Add(ref numerator, ref denominator, from, value, 1); var momentum = numerator.Ratio(denominator); var gain = _alpha * Math.Abs(momentum / 100);
            var result = VidyaBlend.Compute(_seeded ? _mean : value, value, gain);
            if (commit) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((from, value)); _numerator = numerator; _denominator = denominator; _previous = value; _mean = result; _seeded = true; } return result;
        }
        internal void Reset() { _history.Clear(); _numerator = _denominator = default; _previous = _mean = 0; _seeded = false; }
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
