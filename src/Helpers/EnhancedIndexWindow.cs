using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EnhancedIndexWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length; private readonly Average? _mean, _signal;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index; private BigInteger _previousSpread;
    internal static int MeanLength(int length) { length = Math.Max(1, length); return Math.Max(2, Math.Min(530, length / 2 + length % 2)); }
    internal EnhancedIndexWindow(MovingAvgType kind, int length, int signalLength, bool external = false)
    { _length = Math.Max(1, length); if (!external) { _mean = new(kind, MeanLength(_length)); _signal = new(kind, Math.Max(1, signalLength)); } }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _length + 1L; var first = deque.First;
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
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double price, bool final,
        double? externalMean = null, double? externalSignal = null, bool includeSignal = true)
    {
        var highest = Extreme(_highs, high, true, final); var lowest = Extreme(_lows, low, false, final);
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(new(price), final);
        var range = ExactVarianceWindow.Units(highest) - ExactVarianceWindow.Units(lowest);
        var numerator = 2 * (ExactVarianceWindow.Units(price) - Units(mean)) * Unit;
        var line = range.IsZero ? default : Value(range.Sign < 0 ? RocBankValue.RoundUnits(-numerator, -range) : RocBankValue.RoundUnits(numerator, range));
        var signal = !includeSignal ? default : externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(line, final);
        var spread = Units(line) - Units(signal);
        var trade = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _index++; _previousSpread = spread; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal static (double[] Mean, double[] Line, double[] Signal) Components(StockData data, List<double> input, List<double> high, List<double> low,
        MovingAvgType kind, int length, int signalLength, bool callbacks, bool includeSignal)
    {
        var caller = data.CaptureInputSeries();
        double[] Mean(List<double> values, int period) => (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray();
        var mean = Mean(input, MeanLength(length)); var line = new double[input.Count];
        using var window = new EnhancedIndexWindow(kind, length, signalLength, true);
        for (var i = 0; i < input.Count; i++) line[i] = window.Next(high[i], low[i], input[i], true, mean[i], includeSignal: false).Line;
        var signal = includeSignal ? Mean(line.ToList(), Math.Max(1, signalLength)) : Array.Empty<double>();
        data.RestoreInputSeries(caller); return (mean, line, signal);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _mean?.Reset(); _signal?.Reset(); _index = 0; _previousSpread = default; }
    public void Dispose() { _mean?.Dispose(); _signal?.Dispose(); }
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
