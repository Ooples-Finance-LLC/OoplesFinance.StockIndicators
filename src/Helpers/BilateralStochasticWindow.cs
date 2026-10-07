using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class BilateralStochasticWindow : IDisposable
{
    private readonly int _length; private readonly Average? _mean, _rangeMean, _signal;
    private readonly LinkedList<(long Index, RocBankValue Value)> _highs = new(), _lows = new(); private long _count;
    internal BilateralStochasticWindow(MovingAvgType kind, int length, int signalLength, bool external = false)
    { _length = Math.Max(1, length); if (!external) { _mean = new(kind, _length); _rangeMean = new(kind, _length); _signal = new(kind, Math.Max(1, signalLength)); } }
    private static int Compare(RocBankValue a, RocBankValue b) { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return sum.Sign; }
    private RocBankValue Extreme(LinkedList<(long Index, RocBankValue Value)> deque, RocBankValue value, bool maximum, bool final)
    {
        var expiry = _count - _length + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : (maximum ? Compare(value, first.Value.Value) > 0 : Compare(value, first.Value.Value) < 0) ? value : first.Value.Value;
        if (final) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? Compare(last.Value.Value, value) <= 0 : Compare(last.Value.Value, value) >= 0)) deque.RemoveLast(); deque.AddLast((_count, value)); } return result;
    }
    private static RocBankValue Ratio(RocBankValue a, RocBankValue b, RocBankValue denominator, bool absolute = false)
    {
        if (denominator.Mantissa == 0) return default;
        var numerator = new ExactMeanAccumulator(); a.AddTo(ref numerator); b.AddTo(ref numerator, -1); numerator.ScaleByPowerOfTwo(-denominator.UpperShift);
        var result = RocBankValue.Round(numerator, denominator.Mantissa); return absolute ? new(Math.Abs(result.Mantissa), result.UpperShift) : result;
    }
    internal (double Bull, double Bear, double Bso, double SignalLine, Signal Signal, double Range) Next(double value, bool final, double? externalMean = null, double? externalRange = null, double? externalSignal = null)
    {
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(new(value), final); var high = Extreme(_highs, mean, true, final); var low = Extreme(_lows, mean, false, final);
        var difference = new ExactMeanAccumulator(); high.AddTo(ref difference); low.AddTo(ref difference, -1); var range = RocBankValue.Round(difference);
        var scale = externalRange.HasValue ? new RocBankValue(externalRange.Value) : _rangeMean!.Next(range, final);
        var bull = Ratio(mean, low, scale); var bear = Ratio(mean, high, scale, true); var line = Compare(bull, bear) > 0 ? bull : bear;
        var signalLine = externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(line, final);
        var bullish = Compare(bull, bear) > 0 || Compare(bull, signalLine) > 0; var bearish = Compare(bear, bull) > 0 || Compare(bull, signalLine) < 0;
        var signal = bullish ? Signal.Buy : bearish ? Signal.Sell : Signal.None; if (final) _count++;
        return (bull.Publish(), bear.Publish(), line.Publish(), signalLine.Publish(), signal, range.Publish());
    }
    internal static double[][] Components(StockData data, List<double> input, MovingAvgType kind, int length, int signalLength)
    {
        var caller = data.CaptureInputSeries();
        double[] Average(List<double> values, int period) { var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, period), values).ToArray(); data.RestoreInputSeries(caller); return result; }
        var mean = Average(input, length); using var window = new BilateralStochasticWindow(kind, length, signalLength, true); var ranges = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) ranges.Add(window.Next(input[i], true, mean[i], 0, 0).Range);
        var scale = Average(ranges, length); window.Reset(); var maxima = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) maxima.Add(window.Next(input[i], true, mean[i], scale[i], 0).Bso);
        var signal = Average(maxima, signalLength); return new[] { mean, scale, signal };
    }
    internal void Reset() { _mean?.Reset(); _rangeMean?.Reset(); _signal?.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; }
    public void Dispose() { _mean?.Dispose(); _rangeMean?.Dispose(); _signal?.Dispose(); }
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
