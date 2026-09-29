using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;

// Each stage retains a rounded high part and rounded residual. In particular, the
// second stochastic must see the low part of the smoothed first stochastic.
internal sealed class SchaffCycleKernel : IDisposable
{
    private readonly Average? _fast, _slow;
    private readonly int _cycle, _d1, _d2;
    private readonly LinkedList<(long Index, Pair Value)> _macdHigh = new(), _macdLow = new(), _middleHigh = new(), _middleLow = new(), _scales = new();
    private Pair _previousMiddle, _previousOutput, _previousFirst, _previousSecond;
    private long _count; private double _published; private ExactMeanAccumulator _slope;
    internal SchaffCycleKernel(MovingAvgType kind, int fast, int slow, int cycle, int d1, int d2, bool external = false)
    { _cycle = Math.Max(1, cycle); _d1 = Math.Max(1, d1); _d2 = Math.Max(1, d2); if (!external) { _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow)); } }
    private static int Compare(Pair a, Pair b) { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return sum.Sign; }
    private Pair Extreme(LinkedList<(long Index, Pair Value)> deque, Pair value, bool maximum, bool final)
    {
        var expiry = _count - _cycle + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : (maximum ? Compare(value, first.Value.Value) > 0 : Compare(value, first.Value.Value) < 0) ? value : first.Value.Value;
        if (final) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? Compare(last.Value.Value, value) <= 0 : Compare(last.Value.Value, value) >= 0)) deque.RemoveLast(); deque.AddLast((_count, value)); } return result;
    }
    private static Pair Smooth(Pair value, Pair previous, int period)
    { var sum = new ExactMeanAccumulator(); previous.AddTo(ref sum, period - 1L); value.AddTo(ref sum, 2); return Pair.Round(sum, period + 1L); }
    private static Pair Magnitude(Pair value) { var rounded = value.Rounded(); return new(new RocBankValue(Math.Abs(rounded.Mantissa), rounded.UpperShift)); }
    private static Pair Normalize(Pair value, Pair low, Pair high, Pair magnitude, Pair previous)
    {
        var range = Pair.Subtract(high, low); var threshold = magnitude.Rounded().Multiply(1.4210854715202004e-14);
        if (Compare(new(range.Rounded()), new(threshold)) <= 0) return previous;
        var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator, 100); low.AddTo(ref numerator, -100); var denominator = new ExactMeanAccumulator(); range.AddTo(ref denominator);
        var highPart = numerator.Ratio(denominator); var remainder = numerator; range.AddProductTo(ref remainder, -highPart); var lowPart = remainder.Ratio(denominator); var normalized = new Pair(new(highPart), new(lowPart));
        return Compare(normalized, default) < 0 ? default : Compare(normalized, new(new(100))) > 0 ? new(new(100)) : normalized;
    }
    internal (double Stc, double Macd, Signal Signal) Next(double value, bool final, double? externalFast = null, double? externalSlow = null)
    {
        var fast = externalFast.HasValue ? new Pair(new(externalFast.Value)) : _fast!.Next(value, final); var slow = externalSlow.HasValue ? new Pair(new(externalSlow.Value)) : _slow!.Next(value, final);
        var macd = Pair.Subtract(fast, slow); var scaleSum = new ExactMeanAccumulator(); Magnitude(fast).AddTo(ref scaleSum); Magnitude(slow).AddTo(ref scaleSum); var scale = Extreme(_scales, new(RocBankValue.Round(scaleSum)), true, final);
        var first = Normalize(macd, Extreme(_macdLow, macd, false, final), Extreme(_macdHigh, macd, true, final), scale, _previousFirst);
        var middle = Smooth(first, _previousMiddle, _d1); var low = Extreme(_middleLow, middle, false, final); var high = Extreme(_middleHigh, middle, true, final); var lowMagnitude = Magnitude(low); var highMagnitude = Magnitude(high);
        var second = Normalize(middle, low, high, Compare(lowMagnitude, highMagnitude) > 0 ? lowMagnitude : highMagnitude, _previousSecond); var output = Smooth(second, _previousOutput, _d2);
        var stc = Math.Max(0, Math.Min(100, output.Publish())); var slope = new ExactMeanAccumulator(); slope.Add(stc); slope.Add(_published, -1); var change = slope; change.Subtract(_slope);
        var signal = slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _count++; _previousFirst = first; _previousSecond = second; _previousMiddle = middle; _previousOutput = output; _published = stc; _slope = slope; } return (stc, macd.Publish(), signal);
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _macdHigh.Clear(); _macdLow.Clear(); _middleHigh.Clear(); _middleLow.Clear(); _scales.Clear(); _previousFirst = _previousSecond = _previousMiddle = _previousOutput = default; _count = 0; _published = 0; _slope = default; }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); }
    private readonly struct Pair
    {
        private readonly RocBankValue _high, _low;
        internal Pair(RocBankValue high, RocBankValue low = default) { _high = high; _low = low; }
        internal void AddTo(ref ExactMeanAccumulator sum, long weight = 1) { _high.AddTo(ref sum, weight); _low.AddTo(ref sum, weight); }
        internal void AddProductTo(ref ExactMeanAccumulator sum, double factor)
        { var high = new ExactMeanAccumulator(); high.AddProduct(_high.Mantissa, factor); high.ScaleByPowerOfTwo(_high.UpperShift); var low = new ExactMeanAccumulator(); low.AddProduct(_low.Mantissa, factor); low.ScaleByPowerOfTwo(_low.UpperShift); var negative = new ExactMeanAccumulator(); negative.Subtract(high); negative.Subtract(low); sum.Subtract(negative); }
        internal RocBankValue Rounded() { var sum = new ExactMeanAccumulator(); AddTo(ref sum); return RocBankValue.Round(sum); }
        internal double Publish() { var sum = new ExactMeanAccumulator(); AddTo(ref sum); return sum.Mean(1); }
        internal static Pair Round(ExactMeanAccumulator numerator, long divisor = 1) { var high = RocBankValue.Round(numerator, count: divisor); high.AddTo(ref numerator, -divisor); return new(high, RocBankValue.Round(numerator, count: divisor)); }
        internal static Pair Subtract(Pair a, Pair b) { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return Round(sum); }
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length; private readonly SpreadAverage? _legacy;
        private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum, _weighted; private Pair _previous; private long _count;
        internal Average(MovingAvgType kind, int length) { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _legacy = new(kind, length); }
        internal Pair Next(double value, bool final)
        {
            if (_legacy is not null) { var legacy = _legacy.Next(new(value), final); return new(new(legacy.High), new(legacy.Low)); }
            var sum = _sum; var weighted = _weighted; var numerator = new ExactMeanAccumulator(); long divisor; var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window) { weighted.Subtract(sum); weighted.Add(value, _length); if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value); if (_kind == MovingAvgType.WeightedMovingAverage) { numerator = weighted; divisor = (long)_length * (_length + 1L) / 2; } else { if (_count + 1 >= _length) numerator = sum; divisor = _length; } }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum.Add(value); numerator = sum; divisor = _count + 1; }
            else { var ema = _kind == MovingAvgType.ExponentialMovingAverage; _previous.AddTo(ref numerator, _length - 1L); numerator.Add(value, ema ? 2 : 1); divisor = ema ? _length + 1L : _length; }
            var result = Pair.Round(numerator, divisor); if (final) { _sum = sum; _weighted = weighted; _previous = result; _count++; if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } } return result;
        }
        internal void Reset() { _legacy?.Reset(); _history.Clear(); _sum = _weighted = default; _previous = default; _count = 0; }
        public void Dispose() => _legacy?.Dispose();
    }
}
