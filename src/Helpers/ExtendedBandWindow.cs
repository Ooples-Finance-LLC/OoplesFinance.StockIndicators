namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ExtendedBandWindow
{
    private readonly int _length;
    private double _upper, _lower;
    private bool _hasPrevious;
    internal ExtendedBandWindow(int length) { _length = Math.Max(3, length); }
    private double Blend(double dominant, double other)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(dominant, _length - 1); sum.Add(other, 2); return sum.Mean(_length + 1L);
    }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var previousUpper = _hasPrevious ? _upper : close; var previousLower = _hasPrevious ? _lower : close;
        var upper = Blend(Math.Max(close, previousUpper), Math.Min(close, previousUpper));
        var lower = Blend(Math.Min(close, previousLower), Math.Max(close, previousLower));
        var middle = new ExactMeanAccumulator(); middle.Add(upper); middle.Add(lower);
        if (commit) { _upper = upper; _lower = lower; _hasPrevious = true; }
        return (upper, middle.Mean(2), lower);
    }
    internal void Reset() { _upper = _lower = 0; _hasPrevious = false; }
}
