namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SettingLessStepWindow
{
    private RocBankValue _sum, _width, _held;
    private long _count;
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Abs(RocBankValue value) => new(Math.Abs(value.Mantissa), value.UpperShift);
    private static int Compare(RocBankValue a, RocBankValue b)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return sum.Sign; }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var previous = _count == 0 ? current : _held;
        var distance = Abs(Add(current, previous, -1)); var denominator = new ExactMeanAccumulator(); Add(distance, _width).AddTo(ref denominator);
        var numerator = new ExactMeanAccumulator(); distance.AddTo(ref numerator); var gain = numerator.Ratio(denominator);
        var line = Add(current.Multiply(gain), previous.Multiply(1 - gain));
        var sum = Add(_sum, Abs(Add(line, previous, -1))); var total = new ExactMeanAccumulator(); sum.AddTo(ref total);
        var width = RocBankValue.Round(total, count: _count + 1).Multiply(1 + gain);
        var result = Compare(line, Add(previous, width)) > 0 || Compare(line, Add(previous, width, -1)) < 0 ? line : previous;
        if (commit) { _sum = sum; _width = width; _held = result; _count++; }
        return result.Publish();
    }
    internal void Reset() { _sum = _width = _held = default; _count = 0; }
}
