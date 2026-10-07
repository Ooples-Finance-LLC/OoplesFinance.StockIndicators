using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class FearGreedWindow : IDisposable
{
    private readonly Average _fastUp, _fastDown, _slowUp, _slowDown, _signal;
    private double _previousPrice;
    private bool _hasPrevious;
    private RocBankValue _previousSignal;
    internal FearGreedWindow(MovingAvgType kind, int fast, int slow, int signal)
    { _fastUp = new(kind, Math.Max(1, fast)); _fastDown = new(kind, Math.Max(1, fast)); _slowUp = new(kind, Math.Max(1, slow)); _slowDown = new(kind, Math.Max(1, slow)); _signal = new(kind, Math.Max(1, signal)); }
    private static RocBankValue Difference(RocBankValue first, RocBankValue second)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, -1); return RocBankValue.Round(sum); }
    internal static RocBankValue TrueRange(double high, double low, double previous)
    {
        var span = new ExactMeanAccumulator(); span.Add(high); span.Add(low, -1);
        var upper = new ExactMeanAccumulator(); upper.Add(high); upper.Add(previous, -1);
        var lower = new ExactMeanAccumulator(); lower.Add(low); lower.Add(previous, -1);
        if (upper.Sign < 0) { var opposite = new ExactMeanAccumulator(); opposite.Subtract(upper); upper = opposite; }
        if (lower.Sign < 0) { var opposite = new ExactMeanAccumulator(); opposite.Subtract(lower); lower = opposite; }
        var comparison = upper; comparison.Subtract(span); if (comparison.Sign > 0) span = upper;
        comparison = lower; comparison.Subtract(span); if (comparison.Sign > 0) span = lower;
        return RocBankValue.Round(span);
    }
    private static (RocBankValue Up, RocBankValue Down) Direction(double high, double low, double price, double previous)
    { var range = TrueRange(high, low, previous); return (price > previous ? range : default, price < previous ? range : default); }
    private static RocBankValue Line(RocBankValue fastUp, RocBankValue fastDown, RocBankValue slowUp, RocBankValue slowDown)
        => Difference(Difference(fastUp, fastDown), Difference(slowUp, slowDown));
    private static Signal Trade(RocBankValue signal, RocBankValue previous)
    {
        var change = new ExactMeanAccumulator(); signal.AddTo(ref change); previous.AddTo(ref change, -1);
        return signal.Mantissa > 0 && change.Sign > 0 ? Signal.StrongBuy : signal.Mantissa < 0 && change.Sign < 0 ? Signal.StrongSell
            : signal.Mantissa > 0 ? Signal.Buy : signal.Mantissa < 0 ? Signal.Sell : Signal.None;
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double price, bool final)
    {
        var previous = _hasPrevious ? _previousPrice : price;
        var direction = Direction(high, low, price, previous);
        var line = Line(_fastUp.Next(direction.Up, final), _fastDown.Next(direction.Down, final), _slowUp.Next(direction.Up, final), _slowDown.Next(direction.Down, final));
        var signal = _signal.Next(line, final); var trade = Trade(signal, _previousSignal);
        if (final) { _previousPrice = price; _hasPrevious = true; _previousSignal = signal; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, List<double> prices, List<double> highs, List<double> lows,
        MovingAvgType kind, int fast, int slow, int signalLength, bool callbacks, bool includeSignal)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signalLength = Math.Max(1, signalLength);
        var caller = data.CaptureInputSeries();
        RocBankValue[] Mean(RocBankValue[] source, int period)
        {
            var published = callbacks || !StrengthWindow.Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, period) : null;
            if (custom is not null) return custom.Select(v => new RocBankValue(v)).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, period, published!.ToList()).Select(v => new RocBankValue(v)).ToArray();
            using var mean = new Average(kind, period); return source.Select(v => mean.Next(v, true)).ToArray();
        }
        try
        {
            var up = new RocBankValue[prices.Count]; var down = new RocBankValue[prices.Count];
            for (var i = 0; i < prices.Count; i++) { var previous = i == 0 ? prices[i] : prices[i - 1]; (up[i], down[i]) = Direction(highs[i], lows[i], prices[i], previous); }
            var fastUp = Mean(up, fast); var fastDown = Mean(down, fast); var slowUp = Mean(up, slow); var slowDown = Mean(down, slow);
            var line = Enumerable.Range(0, prices.Count).Select(i => Line(fastUp[i], fastDown[i], slowUp[i], slowDown[i])).ToArray();
            var signal = includeSignal ? Mean(line, signalLength) : new RocBankValue[prices.Count];
            var trades = Enumerable.Range(0, prices.Count).Select(i => Trade(signal[i], i == 0 ? default : signal[i - 1])).ToArray();
            return (line.Select(v => v.Publish()).ToArray(), signal.Select(v => v.Publish()).ToArray(), trades);
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _fastUp.Reset(); _fastDown.Reset(); _slowUp.Reset(); _slowDown.Reset(); _signal.Reset(); _previousPrice = 0; _hasPrevious = false; _previousSignal = default; }
    public void Dispose() { _fastUp.Dispose(); _fastDown.Dispose(); _slowUp.Dispose(); _slowDown.Dispose(); _signal.Dispose(); }
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
