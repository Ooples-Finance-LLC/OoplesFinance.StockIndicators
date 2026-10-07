using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DirectionalIndexWindow : IDisposable
{
    private readonly RocBankAverage _plus, _minus, _range, _adx;
    private double _high, _low, _close;
    private bool _hasPrevious;
    internal DirectionalIndexWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    { _plus = new(kind, length, capacityHint); _minus = new(kind, length, capacityHint); _range = new(kind, length, capacityHint); _adx = new(kind, length, capacityHint); }
    private static RocBankValue Difference(double a, double b) { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b, -1); return RocBankValue.Round(sum); }
    private static double Percent(RocBankValue numerator, RocBankValue denominator)
    {
        if (denominator.Mantissa == 0) return 0;
        var top = new ExactMeanAccumulator(); numerator.AddTo(ref top, 100); var bottom = new ExactMeanAccumulator(); denominator.AddTo(ref bottom);
        return Math.Max(0, Math.Min(100, top.Ratio(bottom)));
    }
    internal (double Plus, double Minus, double Adx) Next(double high, double low, double close, bool commit)
    {
        var oldHigh = _hasPrevious ? _high : high; var oldLow = _hasPrevious ? _low : low; var oldClose = _hasPrevious ? _close : close;
        var up = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(oldHigh);
        var down = ExactVarianceWindow.Units(oldLow) - ExactVarianceWindow.Units(low);
        var plus = up > down && up.Sign > 0 ? Difference(high, oldHigh) : default;
        var minus = down > up && down.Sign > 0 ? Difference(oldLow, low) : default;
        var range = Difference(high, low);
        var highGap = BigInteger.Abs(ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(oldClose));
        var lowGap = BigInteger.Abs(ExactVarianceWindow.Units(low) - ExactVarianceWindow.Units(oldClose));
        var span = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
        if (highGap > span && highGap >= lowGap) range = high >= oldClose ? Difference(high, oldClose) : Difference(oldClose, high);
        else if (lowGap > span) range = low >= oldClose ? Difference(low, oldClose) : Difference(oldClose, low);
        var tr = _range.Next(range, commit); var positive = Percent(_plus.Next(plus, commit), tr); var negative = Percent(_minus.Next(minus, commit), tr);
        var top = new ExactMeanAccumulator(); top.Add(positive); top.Add(negative, -1);
        var bottom = new ExactMeanAccumulator(); bottom.Add(positive); bottom.Add(negative);
        var dx = bottom.IsExactlyZero ? 0 : Math.Min(100, Math.Abs(top.Ratio(bottom)) * 100);
        var adx = _adx.Next(new RocBankValue(dx), commit).Publish();
        if (commit) { _high = high; _low = low; _close = close; _hasPrevious = true; }
        return (positive, negative, adx);
    }
    internal void Reset() { _plus.Reset(); _minus.Reset(); _range.Reset(); _adx.Reset(); _high = _low = _close = 0; _hasPrevious = false; }
    public void Dispose() { _plus.Dispose(); _minus.Dispose(); _range.Dispose(); _adx.Dispose(); }
}
