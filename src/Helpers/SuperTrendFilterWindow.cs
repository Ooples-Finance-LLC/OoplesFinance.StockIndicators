namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SuperTrendFilterWindow
{
    private readonly double _gain, _factor;
    private RocBankValue _line, _older, _width, _source, _lower, _upper;
    private bool _seeded, _up = true;
    internal SuperTrendFilterWindow(int length, double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        var resolved = Math.Max(1, length); _gain = 2 / ((double)resolved * resolved + 1); _factor = factor;
    }
    private static RocBankValue Sum(RocBankValue left, RocBankValue right, int sign = 1)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Blend(RocBankValue left, double weight, RocBankValue right, double otherWeight)
    {
        var sum = new ExactMeanAccumulator(); sum.AddProduct(left.Mantissa, weight); sum.ScaleByPowerOfTwo(left.UpperShift);
        var other = new ExactMeanAccumulator(); other.AddProduct(right.Mantissa, otherWeight, -1); other.ScaleByPowerOfTwo(right.UpperShift); sum.Subtract(other); return RocBankValue.Round(sum);
    }
    private static int Compare(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, -1); return sum.Sign; }
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var previous = _seeded ? _line : current;
        var signed = Sum(current, previous, -1); var distance = new RocBankValue(Math.Abs(signed.Mantissa), signed.UpperShift);
        var width = Blend(distance, _gain, _seeded ? _width : distance, 1 - _gain);
        var source = Blend(previous, _factor, current, 1 - _factor);
        var lower = Sum(previous, width, -1); var upper = Sum(previous, width);
        if (Compare(_source, _lower) > 0 && Compare(lower, _lower) < 0) lower = _lower;
        if (Compare(_source, _upper) < 0 && Compare(upper, _upper) > 0) upper = _upper;
        var up = Compare(source, _upper) > 0 ? true : Compare(source, _lower) < 0 ? false : _up;
        var line = up ? upper : lower;
        var difference = new ExactMeanAccumulator(); line.AddTo(ref difference); previous.AddTo(ref difference, -1);
        var change = difference; previous.AddTo(ref change, -1); _older.AddTo(ref change);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _older = _line; _line = line; _width = width; _source = source; _lower = lower; _upper = upper; _up = up; _seeded = true; }
        return (line.Publish(), signal);
    }
    internal void Reset() { _line = _older = _width = _source = _lower = _upper = default; _seeded = false; _up = true; }
}
