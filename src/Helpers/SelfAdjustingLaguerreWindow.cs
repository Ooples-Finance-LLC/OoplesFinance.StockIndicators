namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SelfAdjustingLaguerreWindow
{
    private readonly int _length;
    private readonly LinkedList<(double Value, long Index)> _highs = new(), _lows = new();
    private readonly Queue<Scaled> _ratios = new();
    private ExactMeanAccumulator _ratioSum;
    private long _index;
    private double _previous;
    private Scaled _l0, _l1, _l2, _l3;
    internal SelfAdjustingLaguerreWindow(int length) => _length = Math.Max(1, length);
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
    private static Scaled Divide(ExactMeanAccumulator top, ExactMeanAccumulator bottom)
    {
        if (bottom.IsExactlyZero) return default;
        var numerator = Scaled.Round(top); var denominator = Scaled.Round(bottom);
        var left = new ExactMeanAccumulator(); left.Add(numerator.Mantissa); var right = new ExactMeanAccumulator(); right.Add(denominator.Mantissa);
        var quotient = new ExactMeanAccumulator(); quotient.Add(left.Ratio(right)); quotient.ScaleByPowerOfTwo(numerator.Shift - denominator.Shift); return Scaled.Round(quotient);
    }
    private double Extreme(LinkedList<(double Value, long Index)> deque, double value, bool maximum, bool commit)
    {
        var expires = _index - _length; var node = deque.First;
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
    private static Scaled Stage(Scaled current, Scaled oldInput, Scaled oldOutput, double gain)
    {
        var sum = new ExactMeanAccumulator(); oldInput.Add(ref sum); oldOutput.Add(ref sum); current.Add(ref sum, -1);
        current.Add(ref sum, gain); oldOutput.Add(ref sum, -gain); return Scaled.Round(sum);
    }
    private static void Direction(Scaled left, Scaled right, ref ExactMeanAccumulator positive, ref ExactMeanAccumulator variation)
    {
        var delta = new ExactMeanAccumulator(); left.Add(ref delta); right.Add(ref delta, -1);
        if (delta.Sign < 0) variation.Subtract(delta);
        else { var negative = new ExactMeanAccumulator(); negative.Subtract(delta); variation.Subtract(negative); positive.Subtract(negative); }
    }
    internal double Next(double price, double open, double high, double low, bool commit)
    {
        var highest = Extreme(_highs, high, true, commit); var lowest = Extreme(_lows, low, false, commit);
        var hc = Math.Max(high, _previous); var lc = Math.Min(low, _previous);
        var numerator = new ExactMeanAccumulator(); numerator.Add(hc); numerator.Add(lc, -1);
        var denominator = new ExactMeanAccumulator(); denominator.Add(highest); denominator.Add(lowest, -1); var ratio = Divide(numerator, denominator);
        var ratioSum = _ratioSum; ratio.Add(ref ratioSum); if (_ratios.Count == _length) _ratios.Peek().Add(ref ratioSum, -1);
        var sum = Scaled.Round(ratioSum);
        var gain = sum.Mantissa <= 0 ? .01 : _length == 1 ? .99 : Math.Max(.01, Math.Min(.99, (Math.Log(sum.Mantissa) + sum.Shift * Math.Log(2)) / Math.Log(_length)));
        var source = new ExactMeanAccumulator(); source.Add(open); source.Add(_previous); source.Add(hc, 2); source.Add(lc, 2); source.Add(price, 2); source.ScaleByPowerOfTwo(-3);
        var first = new ExactMeanAccumulator(); _l0.Add(ref first); Scaled.Round(source).Add(ref first, gain); _l0.Add(ref first, -gain); var l0 = Scaled.Round(first);
        var l1 = Stage(l0, _l0, _l1, gain); var l2 = Stage(l1, _l1, _l2, gain); var l3 = Stage(l2, _l2, _l3, gain);
        var positive = new ExactMeanAccumulator(); var variation = new ExactMeanAccumulator(); Direction(l0, l1, ref positive, ref variation); Direction(l1, l2, ref positive, ref variation); Direction(l2, l3, ref positive, ref variation);
        var scale = new Scaled(Math.Abs(l0.Mantissa), l0.Shift);
        foreach (var stage in new[] { l1, l2, l3 })
        { var magnitude = new Scaled(Math.Abs(stage.Mantissa), stage.Shift); var comparison = new ExactMeanAccumulator(); magnitude.Add(ref comparison); scale.Add(ref comparison, -1); if (comparison.Sign > 0) scale = magnitude; }
        var deadband = variation; scale.Add(ref deadband, -1.4210854715202004e-14); var result = deadband.Sign <= 0 ? 0 : positive.Ratio(variation);
        if (commit)
        {
            if (_ratios.Count == _length) _ratios.Dequeue(); _ratios.Enqueue(ratio); _ratioSum = ratioSum;
            _previous = price; _index++; _l0 = l0; _l1 = l1; _l2 = l2; _l3 = l3;
        }
        return result;
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _ratios.Clear(); _ratioSum = default; _previous = 0; _index = 0; _l0 = _l1 = _l2 = _l3 = default; }
}
