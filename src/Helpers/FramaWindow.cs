namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FramaWindow
{
    private readonly int _length, _half;
    private readonly LinkedList<(double Value, long Index)> _highs = new(), _lows = new(), _halfHighs = new(), _halfLows = new();
    private readonly Queue<ExactMeanAccumulator> _lagged = new();
    private long _index;
    private double _gain = 1;
    private Scaled _filter;
    internal FramaWindow(int length)
    { length = Math.Max(2, length); _length = checked(length + (length & 1)); _half = _length / 2; }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(1); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(1); }
            return new(value, shift);
        }
    }
    private double Extreme(LinkedList<(double Value, long Index)> deque, double value, int length, bool maximum, bool commit)
    {
        var expires = _index - length; var node = deque.First;
        while (node != null && node.Value.Index <= expires) node = node.Next;
        var result = node == null ? value : maximum ? Math.Max(value, node.Value.Value) : Math.Min(value, node.Value.Value);
        if (commit)
        {
            while (deque.First != null && deque.First.Value.Index <= expires) deque.RemoveFirst();
            while (deque.Last != null && (maximum ? deque.Last.Value.Value <= value : deque.Last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((value, _index));
        }
        return result;
    }
    private static ExactMeanAccumulator Range(double high, double low)
    { var range = new ExactMeanAccumulator(); range.Add(high); range.Add(low, -1); return range; }
    internal double Next(double price, double high, double low, bool commit)
    {
        var full = Range(Extreme(_highs, high, _length, true, commit), Extreme(_lows, low, _length, false, commit));
        var recent = Range(Extreme(_halfHighs, high, _half, true, commit), Extreme(_halfLows, low, _half, false, commit));
        var older = _lagged.Count == 0 ? recent : _lagged.Peek(); var gain = _gain;
        if (_index >= _length - 1 && recent.Sign > 0 && older.Sign > 0 && full.Sign > 0)
        {
            var negativeOlder = new ExactMeanAccumulator(); negativeOlder.Subtract(older); var sum = recent; sum.Subtract(negativeOlder); sum.ScaleByPowerOfTwo(1);
            var ratio = sum.Ratio(full);
            // Ratios at or below one give a gain of one, even below binary64's exponent range.
            gain = ratio <= 1 ? 1 : Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (Math.Log(ratio) / Math.Log(2) - 1))));
        }
        var source = new Scaled(price, 0); var filter = source;
        if (_index >= _length)
        { var sum = new ExactMeanAccumulator(); _filter.Add(ref sum); source.Add(ref sum, gain); _filter.Add(ref sum, -gain); filter = Scaled.Round(sum); }
        var output = new ExactMeanAccumulator(); filter.Add(ref output); var result = output.Mean(1);
        if (commit)
        { if (_lagged.Count == _half) _lagged.Dequeue(); _lagged.Enqueue(recent); _filter = filter; _gain = gain; _index++; }
        return result;
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _halfHighs.Clear(); _halfLows.Clear(); _lagged.Clear(); _index = 0; _gain = 1; _filter = default; }
}
