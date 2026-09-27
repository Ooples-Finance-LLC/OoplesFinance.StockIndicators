namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MotionAttractionWindow
{
    private readonly int _length;
    private double _a, _b, _upper, _lower;
    private int _upperSteps, _lowerSteps;
    private bool _hasPrevious, _long;
    internal MotionAttractionWindow(int length) { _length = Math.Max(1, length); }
    private double Blend(double anchor, double midpoint, int steps)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(anchor, _length - steps); sum.Add(midpoint, steps); return sum.Mean(_length);
    }
    private static double Midpoint(double upper, double lower) { var sum = new ExactMeanAccumulator(); sum.Add(upper); sum.Add(lower); return sum.Mean(2); }
    internal (double Upper, double Middle, double Lower, double Stop) Next(double close, bool commit)
    {
        var previousA = _hasPrevious ? _a : close; var previousB = _hasPrevious ? _b : close;
        var previousUpper = _hasPrevious ? _upper : close; var previousLower = _hasPrevious ? _lower : close;
        var a = close > previousUpper ? close : previousA; var b = close < previousLower ? close : previousB;
        var aChanged = a != previousA; var bChanged = b != previousB;
        // Count exact 1/length steps so attraction saturates on the length-th change.
        var upperSteps = bChanged ? _upperSteps < _length ? _upperSteps + 1 : _length : aChanged ? 0 : _upperSteps;
        var lowerSteps = aChanged ? _lowerSteps < _length ? _lowerSteps + 1 : _length : bChanged ? 0 : _lowerSteps;
        var midpoint = Midpoint(a, b); var upper = Blend(a, midpoint, upperSteps); var lower = Blend(b, midpoint, lowerSteps);
        var isLong = close > previousUpper ? true : close < previousLower ? false : _long;
        if (commit) { _a = a; _b = b; _upper = upper; _lower = lower; _upperSteps = upperSteps; _lowerSteps = lowerSteps; _long = isLong; _hasPrevious = true; }
        return (upper, Midpoint(upper, lower), lower, isLong ? lower : upper);
    }
    internal void Reset() { _a = _b = _upper = _lower = 0; _upperSteps = _lowerSteps = 0; _hasPrevious = _long = false; }
}
