namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GChannelWindow
{
    private readonly int _length;
    private double _upper, _lower;
    internal GChannelWindow(int length) { _length = Math.Max(2, length); }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        // Normalize the entire recurrence once; separately rounding the contraction
        // can invert subnormal bands even though the real-valued recurrence is ordered.
        var upperSum = new ExactMeanAccumulator(); upperSum.Add(Math.Max(close, _upper), _length); upperSum.Add(_upper, -1); upperSum.Add(_lower);
        var lowerSum = new ExactMeanAccumulator(); lowerSum.Add(Math.Min(close, _lower), _length); lowerSum.Add(_upper); lowerSum.Add(_lower, -1);
        var upper = upperSum.Mean(_length); var lower = lowerSum.Mean(_length);
        var middle = new ExactMeanAccumulator(); middle.Add(upper); middle.Add(lower);
        if (commit) { _upper = upper; _lower = lower; }
        return (upper, middle.Mean(2), lower);
    }
    internal void Reset() { _upper = _lower = 0; }
}
