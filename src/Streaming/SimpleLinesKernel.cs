namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class SimpleLinesKernel
{
    private readonly int _length;
    private readonly double _multiplier;
    private double _anchor;
    private long _ticks, _previousTicks;
    private long _count;
    internal SimpleLinesKernel(int length, double multiplier)
    { if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier)); _length = Math.Max(1, length); _multiplier = multiplier; }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal double Next(double value, bool isFinal)
    {
        var anchor = _count == 0 ? value : _anchor;
        var ticks = _ticks;
        if (_count > 0)
        {
            var priceTicks = Add(new RocBankValue(value), new RocBankValue(anchor), -1).Multiply(_length);
            var previous = _count == 1 ? priceTicks : new RocBankValue(_previousTicks);
            var residual = Add(new RocBankValue(ticks), previous, -1).Multiply(_multiplier);
            var displacement = Add(Add(priceTicks, new RocBankValue(ticks), -1), residual).Publish();
            if (displacement > 1) ticks++;
            else if (displacement < -1) ticks--;
        }
        if (isFinal)
        { _anchor = anchor; _previousTicks = _ticks; _ticks = ticks; _count++; }
        return anchor + ticks / (double)_length;
    }
    internal void Reset() { _anchor = 0; _ticks = _previousTicks = 0; _count = 0; }
}
