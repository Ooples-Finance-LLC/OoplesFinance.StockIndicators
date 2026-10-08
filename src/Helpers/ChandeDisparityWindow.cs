using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Average three price-relative disparities as one ratio. Individual legs can
// overflow or lose a common price term even when their average is finite.
internal sealed class ChandeDisparityWindow : IDisposable
{
    private readonly Average? _first, _second, _third;
    private RocBankValue _previous;
    internal ChandeDisparityWindow(MovingAvgType kind, int length1, int length2, int length3, bool external = false)
    { if (!external) { _first = new(kind, Math.Max(1, length1)); _second = new(kind, Math.Max(1, length2)); _third = new(kind, Math.Max(1, length3)); } }
    internal (double Value, Signal Signal) Next(double price, bool final, double? external1 = null, double? external2 = null, double? external3 = null)
    {
        _ = ExactVarianceWindow.Units(price);
        var first = external1.HasValue ? new RocBankValue(external1.Value) : _first!.Next(new(price), final);
        var second = external2.HasValue ? new RocBankValue(external2.Value) : _second!.Next(new(price), final);
        var third = external3.HasValue ? new RocBankValue(external3.Value) : _third!.Next(new(price), final);
        var numerator = new ExactMeanAccumulator(); numerator.Add(price, 300); first.AddTo(ref numerator, -100); second.AddTo(ref numerator, -100); third.AddTo(ref numerator, -100);
        var value = price == 0 ? default : RocBankValue.Round(numerator, price, 3);
        var change = new ExactMeanAccumulator(); value.AddTo(ref change); _previous.AddTo(ref change, -1);
        var level = new ExactMeanAccumulator(); value.AddTo(ref level);
        var signal = level.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : level.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : level.Sign > 0 ? Signal.Buy : level.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previous = value; return (value.Publish(), signal);
    }
    internal static double[][] Components(StockData data, List<double> input, MovingAvgType kind, int length1, int length2, int length3)
    {
        var caller = data.CaptureInputSeries(); var periods = new[] { length1, length2, length3 }; var values = new double[3][];
        for (var i = 0; i < 3; i++) { var period = Math.Max(1, periods[i]); values[i] = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), period)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, period, input).ToArray(); data.RestoreInputSeries(caller); } return values;
    }
    internal void Reset() { _first?.Reset(); _second?.Reset(); _third?.Reset(); _previous = default; }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _third?.Dispose(); }
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
