using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DoubleSmoothedStochasticWindow : IDisposable
{
    private readonly int _length; private readonly Average? _firstNumerator, _firstDenominator, _secondNumerator, _secondDenominator, _signal;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new(); private long _count;
    private double _previous; private ExactMeanAccumulator _spread;
    internal DoubleSmoothedStochasticWindow(MovingAvgType kind, int length1, int length2, int length3, int length4, bool external = false)
    { _length = Math.Max(1, length1); if (!external) { _firstNumerator = new(kind, Math.Max(1, length2)); _firstDenominator = new(kind, Math.Max(1, length2)); _secondNumerator = new(kind, Math.Max(1, length3)); _secondDenominator = new(kind, Math.Max(1, length3)); _signal = new(kind, Math.Max(1, length4)); } }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _length + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast(); deque.AddLast((_count, value)); } return result;
    }
    private (RocBankValue Numerator, RocBankValue Denominator) Parts(double high, double low, double value, bool final)
    {
        var upper = Extreme(_highs, high, true, final); var lower = Extreme(_lows, low, false, final); var numerator = new ExactMeanAccumulator(); numerator.Add(value); numerator.Add(lower, -1); var denominator = new ExactMeanAccumulator(); denominator.Add(upper); denominator.Add(lower, -1);
        if (final) _count++; return (RocBankValue.Round(numerator), RocBankValue.Round(denominator));
    }
    private static double Normalize(RocBankValue numerator, RocBankValue denominator)
    { if (denominator.Mantissa == 0) return 0; var top = new ExactMeanAccumulator(); numerator.AddTo(ref top, 100); var bottom = new ExactMeanAccumulator(); denominator.AddTo(ref bottom); return Math.Max(0, Math.Min(100, top.Ratio(bottom))); }
    internal (double Dss, double SignalLine, Signal Signal) Next(double high, double low, double value, bool final, double? externalNumerator = null, double? externalDenominator = null, double? externalSignal = null)
    {
        var parts = Parts(high, low, value, final);
        var numerator = externalNumerator.HasValue ? new RocBankValue(externalNumerator.Value) : _secondNumerator!.Next(_firstNumerator!.Next(parts.Numerator, final), final);
        var denominator = externalDenominator.HasValue ? new RocBankValue(externalDenominator.Value) : _secondDenominator!.Next(_firstDenominator!.Next(parts.Denominator, final), final);
        var line = Normalize(numerator, denominator); var signalLine = externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(new(line), final);
        var spread = new ExactMeanAccumulator(); spread.Add(line); signalLine.AddTo(ref spread, -1); var change = spread; change.Subtract(_spread);
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 || (_previous < 30 && line > 30) ? Signal.Buy : spread.Sign < 0 || (_previous > 70 && line < 70) ? Signal.Sell : Signal.None;
        if (final) { _previous = line; _spread = spread; } return (line, signalLine.Publish(), signal);
    }
    internal static double[][] Components(StockData data, List<double> input, List<double> high, List<double> low, MovingAvgType kind, int length1, int length2, int length3, int length4)
    {
        var caller = data.CaptureInputSeries();
        double[] Average(List<double> values, int period) { var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, period), values).ToArray(); data.RestoreInputSeries(caller); return result; }
        using var window = new DoubleSmoothedStochasticWindow(kind, length1, length2, length3, length4, true); var rawNumerator = new List<double>(input.Count); var rawDenominator = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var parts = window.Parts(high[i], low[i], input[i], true); rawNumerator.Add(parts.Numerator.Publish()); rawDenominator.Add(parts.Denominator.Publish()); }
        var firstNumerator = Average(rawNumerator, length2); var firstDenominator = Average(rawDenominator, length2); var secondNumerator = Average(firstNumerator.ToList(), length3); var secondDenominator = Average(firstDenominator.ToList(), length3);
        var line = Enumerable.Range(0, input.Count).Select(i => Normalize(new(secondNumerator[i]), new(secondDenominator[i]))).ToList(); var signal = Average(line, length4); return new[] { secondNumerator, secondDenominator, signal };
    }
    internal void Reset() { _firstNumerator?.Reset(); _firstDenominator?.Reset(); _secondNumerator?.Reset(); _secondDenominator?.Reset(); _signal?.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previous = 0; _spread = default; }
    public void Dispose() { _firstNumerator?.Dispose(); _firstDenominator?.Dispose(); _secondNumerator?.Dispose(); _secondDenominator?.Dispose(); _signal?.Dispose(); }
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
