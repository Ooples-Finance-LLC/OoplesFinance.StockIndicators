using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FastSlowCompositeWindow : IDisposable
{
    private readonly bool _rsi, _preserveFlat; private readonly int _levelLength;
    private readonly FastSlowKurtosisWindow? _momentum; private readonly Average? _velocity, _level, _signal, _gains, _losses; private readonly RsiState? _legacyRsi;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new(); private long _count;
    private double _previousPrice, _previousRsi; private ExactMeanAccumulator _spread;
    internal FastSlowCompositeWindow(bool rsi, MovingAvgType kind, int length1, int length2, int length3, int length4, bool external = false)
    {
        _rsi = rsi; _levelLength = Math.Max(1, length3); _preserveFlat = _levelLength > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
        if (!external) { _momentum = new(Math.Max(1, length1), .03, kind); _velocity = new(kind, Math.Max(1, length2)); _signal = new(kind, Math.Max(1, length4)); if (!rsi) _level = new(kind, _levelLength); else if (StrengthWindow.Supports(kind)) { _gains = new(kind, _levelLength); _losses = new(kind, _levelLength); } else _legacyRsi = new(kind, _levelLength); }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _levelLength + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next; var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast(); deque.AddLast((_count, value)); } return result;
    }
    private static double Strength(RocBankValue gain, RocBankValue loss)
    { var total = new ExactMeanAccumulator(); gain.AddTo(ref total); loss.AddTo(ref total); var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100); return total.IsExactlyZero ? 100 : Math.Max(0, Math.Min(100, numerator.Ratio(total))); }
    private static RocBankValue Combine(bool rsi, RocBankValue velocity, RocBankValue level)
    { var scaled = velocity.Multiply(rsi ? 10000 : 500); var sum = new ExactMeanAccumulator(); scaled.AddTo(ref sum); level.AddTo(ref sum); return RocBankValue.Round(sum); }
    internal (double Line, double SignalLine, Signal Signal) Next(double high, double low, double price, bool final, double? externalVelocity = null, double? externalLevel = null, double? externalSignal = null)
    {
        var velocity = externalVelocity.HasValue ? new RocBankValue(externalVelocity.Value) : _velocity!.Next(_momentum!.Line(price, final), final); RocBankValue level;
        if (externalLevel.HasValue) level = new(externalLevel.Value);
        else if (!_rsi) { var upper = Extreme(_highs, high, true, final); var lower = Extreme(_lows, low, false, final); level = _level!.Next(new(ClampedRangePosition.Percent(price, lower, upper)), final); }
        else if (_legacyRsi is not null) level = new(_legacyRsi.Next(price, final));
        else
        {
            var difference = new ExactMeanAccumulator(); if (_count > 0) { difference.Add(price); difference.Add(_previousPrice, -1); } var change = RocBankValue.Round(difference);
            var gain = _gains!.Next(change.Mantissa > 0 ? change : default, final); var loss = _losses!.Next(change.Mantissa < 0 ? new(-change.Mantissa, change.UpperShift) : default, final);
            var strength = _preserveFlat && _count > 0 && price == _previousPrice ? _previousRsi : Strength(gain, loss); level = new(strength); if (final) _previousRsi = strength;
        }
        var line = Combine(_rsi, velocity, level); var signalLine = externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(line, final);
        var spread = new ExactMeanAccumulator(); line.AddTo(ref spread); signalLine.AddTo(ref spread, -1); var changeOfSpread = spread; changeOfSpread.Subtract(_spread);
        var signal = spread.Sign > 0 && changeOfSpread.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && changeOfSpread.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _count++; _previousPrice = price; _spread = spread; } return (line.Publish(), signalLine.Publish(), signal);
    }
    internal static double[][] Components(StockData data, List<double> input, List<double> high, List<double> low, bool rsi, MovingAvgType kind, int length1, int length2, int length3, int length4, bool includeSignal = true)
    {
        var caller = data.CaptureInputSeries();
        double[] Average(List<double> values, int period) { var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, period), values).ToArray(); data.RestoreInputSeries(caller); return result; }
        using var momentum = new FastSlowKurtosisWindow(Math.Max(1, length1), .03, kind); var rawVelocity = input.Select(v => momentum.Line(v, true).Publish()).ToList(); double[] velocity, level;
        if (rsi)
        {
            var gains = new List<double>(input.Count); var losses = new List<double>(input.Count);
            for (var i = 0; i < input.Count; i++) { var change = new ExactMeanAccumulator(); if (i > 0) { change.Add(input[i]); change.Add(input[i - 1], -1); } var value = change.Mean(1); gains.Add(Math.Max(0, value)); losses.Add(Math.Max(0, -value)); }
            var up = Average(gains, length3); var down = Average(losses, length3); level = new double[input.Count];
            for (var i = 0; i < input.Count; i++) level[i] = Math.Max(1, length3) > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod && i > 0 && input[i] == input[i - 1] ? level[i - 1] : StrengthWindow.Supports(kind) ? Strength(new(up[i]), new(down[i])) : down[i] == 0 ? 100 : up[i] == 0 ? 0 : Math.Max(0, Math.Min(100, 100 - 100 / (1 + up[i] / down[i])));
            velocity = Average(rawVelocity, length2);
        }
        else
        {
            velocity = Average(rawVelocity, length2); using var extrema = new FastSlowCompositeWindow(false, kind, length1, length2, length3, length4, true); var rawLevel = new List<double>(input.Count);
            for (var i = 0; i < input.Count; i++) { var upper = extrema.Extreme(extrema._highs, high[i], true, true); var lower = extrema.Extreme(extrema._lows, low[i], false, true); rawLevel.Add(ClampedRangePosition.Percent(input[i], lower, upper)); extrema._count++; }
            level = Average(rawLevel, length3);
        }
        var composite = Enumerable.Range(0, input.Count).Select(i => Combine(rsi, new(velocity[i]), new(level[i])).Publish()).ToList(); var signal = includeSignal ? Average(composite, length4) : new double[input.Count]; return new[] { velocity, level, signal };
    }
    internal void Reset() { _momentum?.Reset(); _velocity?.Reset(); _level?.Reset(); _signal?.Reset(); _gains?.Reset(); _losses?.Reset(); _legacyRsi?.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previousPrice = _previousRsi = 0; _spread = default; }
    public void Dispose() { _momentum?.Dispose(); _velocity?.Dispose(); _level?.Dispose(); _signal?.Dispose(); _gains?.Dispose(); _losses?.Dispose(); _legacyRsi?.Dispose(); }
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
