namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class SimpleLinesKernel
{
    private readonly int _length;
    private readonly double _multiplier;
    private double _anchor;
    private long _ticks, _previousTicks;
    private int _count;
    internal SimpleLinesKernel(int length, double multiplier)
    { _length = Math.Max(1, length); _multiplier = multiplier; }
    internal double Next(double value, bool isFinal)
    {
        var anchor = _count == 0 ? value : _anchor;
        var ticks = _ticks;
        if (_count > 0)
        {
            var priceTicks = (value - anchor) * _length;
            var previous = _count == 1 ? priceTicks : _previousTicks;
            var displacement = priceTicks - ticks + _multiplier * (ticks - previous);
            if (displacement > 1) ticks++;
            else if (displacement < -1) ticks--;
        }
        if (isFinal)
        { _anchor = anchor; _previousTicks = _ticks; _ticks = ticks; _count++; }
        return anchor + ticks / (double)_length;
    }
    internal void Reset() { _anchor = 0; _ticks = _previousTicks = 0; _count = 0; }
}
