using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// DT ranges RSI, then averages available observations twice. Histories grow
// only with incoming bars, including both finite-window RSI average kinds.
internal sealed class DtOscillatorWindow : IDisposable
{
    private readonly int _rangeLength;
    private readonly PriceRsiWindow? _recursive; private readonly RsiState? _legacy;
    private readonly StrengthMean? _gains, _losses; private readonly PartialMean _line, _signal;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count; private double _previousPrice, _previousSignal; private BigInteger _previousSlope;
    internal DtOscillatorWindow(MovingAvgType kind, int rsi, int range, int first, int second, bool external = false)
    {
        rsi = Math.Max(1, rsi); _rangeLength = Math.Max(1, range); _line = new(first); _signal = new(second);
        if (!external)
        {
            if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, rsi);
            else if (StrengthWindow.Supports(kind)) { _gains = new(kind, rsi); _losses = new(kind, rsi); }
            else _legacy = new(kind, rsi);
        }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _rangeLength + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal (double Line, double SignalLine, Signal Signal, double Rsi) Next(double price, bool final, double? externalRsi = null)
    {
        _ = ExactVarianceWindow.Units(price); double rsi;
        if (externalRsi.HasValue) rsi = externalRsi.Value;
        else if (_recursive is not null) rsi = _recursive.Next(price, final);
        else if (_legacy is not null) rsi = _legacy.Next(price, final);
        else
        {
            var difference = new ExactMeanAccumulator(); if (_count > 0) { difference.Add(price); difference.Add(_previousPrice, -1); }
            var change = RocBankValue.Round(difference); var up = _gains!.Next(change.Mantissa > 0 ? change : default, final);
            var down = _losses!.Next(change.Mantissa < 0 ? new RocBankValue(-change.Mantissa, change.UpperShift) : default, final);
            var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100); var total = new ExactMeanAccumulator(); up.AddTo(ref total); down.AddTo(ref total);
            rsi = total.IsExactlyZero ? 100 : Math.Max(0, Math.Min(100, numerator.Ratio(total)));
        }
        var high = Extreme(_highs, rsi, true, final); var low = Extreme(_lows, rsi, false, final);
        var stochastic = ClampedRangePosition.Percent(rsi, low, high);
        var line = _line.Next(stochastic, final); var signalLine = _signal.Next(line, final);
        var slope = ExactVarianceWindow.Units(signalLine) - ExactVarianceWindow.Units(_previousSignal);
        var signal = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 || _previousSignal < 30 && signalLine > 30 ? Signal.Buy
            : slope.Sign < 0 || _previousSignal > 70 && signalLine < 70 ? Signal.Sell : Signal.None;
        if (final) { _count++; _previousPrice = price; _previousSignal = signalLine; _previousSlope = slope; }
        return (line, signalLine, signal, rsi);
    }
    internal void Reset() { _recursive?.Reset(); _legacy?.Reset(); _gains?.Reset(); _losses?.Reset(); _line.Reset(); _signal.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previousPrice = _previousSignal = 0; _previousSlope = default; }
    public void Dispose() { _recursive?.Dispose(); _legacy?.Dispose(); }
    private sealed class PartialMean
    {
        private readonly int _length; private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum;
        internal PartialMean(int length) => _length = Math.Max(1, length);
        internal double Next(double value, bool final)
        {
            var sum = _sum; if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
            var result = sum.Mean(Math.Min(_length, _history.Count + 1L));
            if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); _sum = sum; } return result;
        }
        internal void Reset() { _history.Clear(); _sum = default; }
    }
    private sealed class StrengthMean
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        internal StrengthMean(MovingAvgType kind, int length) { _kind = kind; _length = length; }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _weighted = weighted; } return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; }
    }
}
