namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FlaggingBandWindow : IDisposable
{
    private readonly int _length;
    private readonly ExactPopulationWindow _deviation;
    private double _upper, _lower, _olderUpper, _olderLower;
    private int _history;
    private bool _long;
    internal FlaggingBandWindow(int length) { _length = Math.Max(1, length); _deviation = new(_length); }
    private double Decay(double boundary, double deviation, int sign)
    { var sum = new ExactMeanAccumulator(); sum.Add(boundary, _length); sum.Add(deviation, sign); return sum.Mean(_length); }
    private static double Midpoint(double a, double b) { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b); return sum.Mean(2); }
    internal (double Upper, double Middle, double Lower, double Stop) Next(double close, bool commit)
    {
        var deviation = _deviation.Next(close, commit);
        var previousUpper = _history > 0 ? _upper : close; var previousLower = _history > 0 ? _lower : close;
        var olderUpper = _history >= 2 ? _olderUpper : close; var olderLower = _history >= 2 ? _olderLower : close;
        // Equality means that the retained boundary did not move on the preceding bar.
        var upper = close > previousUpper ? close : previousUpper == olderUpper ? Math.Max(close, Decay(previousUpper, deviation, -1)) : previousUpper;
        var lower = close < previousLower ? close : previousLower == olderLower ? Math.Min(close, Decay(previousLower, deviation, 1)) : previousLower;
        var isLong = close > olderUpper ? true : close < olderLower ? false : _long;
        var middle = Midpoint(isLong ? upper : lower, Midpoint(upper, lower));
        if (commit) { _olderUpper = previousUpper; _olderLower = previousLower; _upper = upper; _lower = lower; _long = isLong; _history = Math.Min(2, _history + 1); }
        return (upper, middle, lower, isLong ? lower : upper);
    }
    internal void Reset() { _deviation.Reset(); _upper = _lower = _olderUpper = _olderLower = 0; _history = 0; _long = false; }
    public void Dispose() => _deviation.Dispose();
}
