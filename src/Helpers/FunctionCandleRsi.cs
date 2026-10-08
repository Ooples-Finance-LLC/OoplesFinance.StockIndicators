using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
// Four independent instances transform the selected close and original candle fields.
internal sealed class FunctionCandleRsi : IDisposable
{
    private readonly Average _gains, _losses;
    private readonly bool _carryFlat;
    private bool _started; private double _previous, _line;
    internal FunctionCandleRsi(MovingAvgType kind, int length)
    { length = Math.Max(1, length); _gains = new(kind, length); _losses = new(kind, length); _carryFlat = length > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod; }
    internal double Next(double price, bool final, double? gainOverride = null, double? lossOverride = null)
    {
        var change = _started ? GainLossShare.Change(price, _previous) : default;
        var gain = gainOverride.HasValue ? new StrengthValue(gainOverride.Value) : _gains.Next(change.Mantissa > 0 ? change : default, final);
        var loss = lossOverride.HasValue ? new StrengthValue(lossOverride.Value) : _losses.Next(change.Mantissa < 0 ? change.Absolute : default, final);
        var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100);
        var denominator = new ExactMeanAccumulator(); gain.AddTo(ref denominator); loss.AddTo(ref denominator);
        var line = _carryFlat && !gainOverride.HasValue && !lossOverride.HasValue && _started && price == _previous ? _line : GainLossShare.Of(numerator, denominator, 100); // NOSONAR: S1244 - Exact equality selects the unchanged-price recurrence.
        if (final) { _previous = price; _line = line; _started = true; } return line;
    }
    internal static double[] Calculate(double[] prices, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); IReadOnlyList<double>? gain = null, loss = null;
        if (callbacks)
        {
            var gains = new double[prices.Length]; var losses = new double[prices.Length];
            for (var i = 1; i < prices.Length; i++)
            { var change = GainLossShare.Change(prices[i], prices[i - 1]); var sum = new ExactMeanAccumulator(); change.Absolute.AddTo(ref sum); if (change.Mantissa > 0) gains[i] = sum.Mean(1); else if (change.Mantissa < 0) losses[i] = sum.Mean(1); }
            gain = ComponentAverage.Take(gains, length); loss = ComponentAverage.Take(losses, length);
        }
        using var window = new FunctionCandleRsi(kind, length); var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++) result[i] = window.Next(prices[i], true, gain is null ? null : gain[i], loss is null ? null : loss[i]);
        return result;
    }
    internal void Reset() { _gains.Reset(); _losses.Reset(); _started = false; _previous = _line = 0; }
    public void Dispose() { _gains.Dispose(); _losses.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<StrengthValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly StrengthAverage? _recursive;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); }
        internal StrengthValue Next(StrengthValue value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(value, final);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? StrengthValue.Round(weighted, (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : StrengthValue.Round(sum, _length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); }
        public void Dispose() => _recursive?.Dispose();
    }
}
