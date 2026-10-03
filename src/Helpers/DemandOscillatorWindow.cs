using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DemandOscillatorWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _rangeLength;
    private readonly Average _range, _line, _signal;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index; private double _previousPrice; private BigInteger _previousSignal, _previousSlope;
    internal DemandOscillatorWindow(MovingAvgType kind, int averageLength, int rangeLength, int lineLength)
    { _rangeLength = Math.Max(1, rangeLength); _range = new(kind, Math.Max(1, averageLength)); _line = new(kind, Math.Max(1, lineLength)); _signal = new(kind, Math.Max(1, averageLength)); }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static BigInteger Round(BigInteger n, BigInteger d) => d.Sign < 0 ? RocBankValue.RoundUnits(-n, -d) : RocBankValue.RoundUnits(n, d);
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _rangeLength + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    internal RocBankValue Range(double high, double low, bool final)
    {
        var h = Extreme(_highs, high, true, final); var l = Extreme(_lows, low, false, final);
        return Value(Round(ExactVarianceWindow.Units(h) - ExactVarianceWindow.Units(l), BigInteger.One));
    }
    internal static RocBankValue Imbalance(double close, double previous, double volume, RocBankValue averageRange)
    {
        var c = ExactVarianceWindow.Units(close); var p = ExactVarianceWindow.Units(previous); var v = ExactVarianceWindow.Units(volume);
        var change = Round(c - p, BigInteger.One);
        var percent = p.IsZero ? BigInteger.Zero : Round(Round(change * Unit, BigInteger.Abs(p)) * 100, BigInteger.One);
        var range = Units(averageRange); var numerator = Round(3 * c, BigInteger.One);
        var k = range.IsZero ? BigInteger.Zero : Round(numerator * Unit, range);
        var product = Round(percent * k, Unit);
        var reciprocal = product.IsZero ? BigInteger.Zero : Round(v * Unit, product);
        return Value(Round(close > previous ? v - reciprocal : reciprocal - v, BigInteger.One));
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double close, double volume, bool final,
        double? externalRange = null, double? externalLine = null, double? externalSignal = null)
    {
        var range = externalRange.HasValue ? new RocBankValue(externalRange.Value) : _range.Next(Range(high, low, final), final);
        var imbalance = Imbalance(close, _previousPrice, volume, range);
        var line = externalLine.HasValue ? new RocBankValue(externalLine.Value) : _line.Next(imbalance, final);
        var signal = externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal.Next(line, final);
        var current = Units(signal); var slope = current - _previousSignal;
        // Compare successive slopes: a rising but slowing signal is Buy, not StrongBuy.
        var priorSlope = _previousSlope;
        var trade = slope.Sign > 0 && slope > priorSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < priorSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _index++; _previousPrice = close; _previousSignal = current; _previousSlope = slope; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal static (double[] Range, double[] Line, double[] Signal) Components(StockData data, List<double> input, List<double> high, List<double> low,
        MovingAvgType kind, int averageLength, int rangeLength, int lineLength, bool callbacks, bool includeSignal)
    {
        averageLength = Math.Max(1, averageLength); lineLength = Math.Max(1, lineLength); var caller = data.CaptureInputSeries();
        using var ranges = new DemandOscillatorWindow(kind, averageLength, rangeLength, lineLength);
        var raw = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { raw.Add(ranges.Range(high[i], low[i], true).Publish()); ranges._index++; }
        double[] Mean(List<double> values, int period) => (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray();
        var range = Mean(raw, averageLength);
        var pressure = Enumerable.Range(0, input.Count).Select(i => Imbalance(input[i], i == 0 ? 0 : input[i - 1], data.Volumes[i], new(range[i])).Publish()).ToList();
        var line = Mean(pressure, lineLength); var signal = includeSignal ? Mean(line.ToList(), averageLength) : Array.Empty<double>();
        data.RestoreInputSeries(caller); return (range, line, signal);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _range.Reset(); _line.Reset(); _signal.Reset(); _index = 0; _previousPrice = 0; _previousSignal = _previousSlope = default; }
    public void Dispose() { _range.Dispose(); _line.Dispose(); _signal.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(value, final);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count < _length - 1 ? default : RocBankValue.Round(sum, count: _length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
