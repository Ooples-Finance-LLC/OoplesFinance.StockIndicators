namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveLaguerreWindow
{
    private readonly int _length, _medianLength;
    private readonly double _initialGain;
    private readonly LinkedList<(Scaled Value, long Index)> _highs = new(), _lows = new();
    private readonly Queue<(double Value, long Index)> _history = new();
    private readonly SortedSet<(double Value, long Index)> _lower = new(), _upper = new();
    private long _index;
    private bool _started;
    private double _gain;
    private Scaled _l0, _l1, _l2, _l3, _filter;
    internal AdaptiveLaguerreWindow(int length, int medianLength)
    { _length = Math.Max(1, length); _medianLength = Math.Max(1, medianLength); _gain = _initialGain = 2d / (_length + 1d); }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum, int count = 1)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(count); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(count); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(count); }
            return new(value, shift);
        }
    }
    private static ExactMeanAccumulator Difference(Scaled left, Scaled right)
    { var sum = new ExactMeanAccumulator(); left.Add(ref sum); right.Add(ref sum, -1); return sum; }
    private static int Compare(Scaled left, Scaled right) => Difference(left, right).Sign;
    private Scaled Extreme(LinkedList<(Scaled Value, long Index)> deque, Scaled value, bool maximum, bool commit)
    {
        var expires = _index - _length; var node = deque.First;
        while (node != null && node.Value.Index <= expires) node = node.Next;
        var result = node == null || (maximum ? Compare(value, node.Value.Value) > 0 : Compare(value, node.Value.Value) < 0) ? value : node.Value.Value;
        if (commit)
        {
            while (deque.First != null && deque.First.Value.Index <= expires) deque.RemoveFirst();
            while (deque.Last != null && (maximum ? Compare(deque.Last.Value.Value, value) <= 0 : Compare(deque.Last.Value.Value, value) >= 0)) deque.RemoveLast();
            deque.AddLast((value, _index));
        }
        return result;
    }
    private void Balance()
    {
        while (_lower.Count > _upper.Count + 1) { var item = _lower.Max; _lower.Remove(item); _upper.Add(item); }
        while (_upper.Count > _lower.Count) { var item = _upper.Min; _upper.Remove(item); _lower.Add(item); }
    }
    private void Insert((double Value, long Index) item)
    { if (_lower.Count == 0 || item.CompareTo(_lower.Max) <= 0) _lower.Add(item); else _upper.Add(item); Balance(); }
    private void Remove((double Value, long Index) item)
    { if (!_lower.Remove(item)) _upper.Remove(item); Balance(); }
    private double Median(double rank, bool commit)
    {
        var full = _history.Count == _medianLength; var expired = full ? _history.Peek() : default;
        if (full) Remove(expired); var current = (rank, _index); Insert(current);
        var mean = new ExactMeanAccumulator(); mean.Add(_lower.Max.Value); var even = _lower.Count == _upper.Count;
        if (even) mean.Add(_upper.Min.Value); var result = mean.Mean(even ? 2 : 1);
        if (commit) { if (full) _history.Dequeue(); _history.Enqueue(current); }
        else { Remove(current); if (full) Insert(expired); }
        return result;
    }
    private static Scaled Stage(Scaled current, Scaled oldInput, Scaled oldOutput, double gain)
    {
        var sum = new ExactMeanAccumulator(); oldInput.Add(ref sum); oldOutput.Add(ref sum); current.Add(ref sum, -1);
        current.Add(ref sum, gain); oldOutput.Add(ref sum, -gain); return Scaled.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var input = new Scaled(price, 0); var previous = _started ? _filter : input;
        var delta = Scaled.Round(Difference(input, previous)); var deviation = new Scaled(Math.Abs(delta.Mantissa), delta.Shift);
        var high = Extreme(_highs, deviation, true, commit); var low = Extreme(_lows, deviation, false, commit);
        var magnitude = new Scaled(Math.Abs(input.Mantissa), input.Shift); var oldMagnitude = new Scaled(Math.Abs(previous.Mantissa), previous.Shift);
        var scale = Compare(magnitude, oldMagnitude) >= 0 ? magnitude : oldMagnitude;
        var range = Difference(high, low); var bottom = Difference(deviation, low); var top = Difference(high, deviation);
        var rangeGuard = range; scale.Add(ref rangeGuard, -7.105427357601002e-15);
        var bottomGuard = bottom; scale.Add(ref bottomGuard, -7.105427357601002e-15);
        var topGuard = top; scale.Add(ref topGuard, -7.105427357601002e-15);
        var rank = rangeGuard.Sign <= 0 || bottomGuard.Sign <= 0 ? 0 : topGuard.Sign <= 0 ? 1 : bottom.Ratio(range);
        var median = Median(rank, commit); var gain = rank != 0 ? median : _gain;
        var p0 = _started ? _l0 : input; var p1 = _started ? _l1 : input; var p2 = _started ? _l2 : input; var p3 = _started ? _l3 : input;
        var first = new ExactMeanAccumulator(); p0.Add(ref first); input.Add(ref first, gain); p0.Add(ref first, -gain); var l0 = Scaled.Round(first);
        var l1 = Stage(l0, p0, p1, gain); var l2 = Stage(l1, p1, p2, gain); var l3 = Stage(l2, p2, p3, gain);
        var sum = new ExactMeanAccumulator(); l0.Add(ref sum); l1.Add(ref sum, 2); l2.Add(ref sum, 2); l3.Add(ref sum); var filter = Scaled.Round(sum, 6);
        var published = new ExactMeanAccumulator(); filter.Add(ref published); var result = published.Mean(1);
        if (commit) { _l0 = l0; _l1 = l1; _l2 = l2; _l3 = l3; _filter = filter; _gain = gain; _started = true; _index++; }
        return result;
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _history.Clear(); _lower.Clear(); _upper.Clear(); _index = 0; _started = false; _gain = _initialGain; _l0 = _l1 = _l2 = _l3 = _filter = default; }
}
