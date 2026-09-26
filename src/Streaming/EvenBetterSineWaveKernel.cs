namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class EvenBetterSineWaveKernel
{
    private readonly double _pole, _gain, _c1, _c2, _c3;
    private double _price, _change, _high, _filter1, _filter2;
    private bool _hasPrice;
    internal EvenBetterSineWaveKernel(int highPeriod, int lowPeriod)
    {
        var angle = Math.Max(.01, Math.Min(.99, 2 * Math.PI / Math.Max(1, highPeriod)));
        _pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        _gain = (1 + _pole) / 2;
        var lowAngle = Math.Max(.01, Math.Min(.99, 1.414 * Math.PI / Math.Max(1, lowPeriod)));
        var radius = Math.Exp(-lowAngle);
        _c2 = 2 * radius * Math.Cos(lowAngle); _c3 = -radius * radius; _c1 = 1 - _c2 - _c3;
    }
    internal double Next(double value, bool final)
    {
        var change = _hasPrice ? value - _price : 0;
        // Commute the two-tap average ahead of the high-pass pole: its alternating
        // steady response then cancels exactly, retaining the decaying transient.
        var high = _gain * (change + _change) / 2 + _pole * _high;
        var filtered = _c1 * high + _c2 * _filter1 + _c3 * _filter2;
        var scale = Math.Max(Math.Abs(filtered), Math.Max(Math.Abs(_filter1), Math.Abs(_filter2)));
        var result = 0d;
        if (scale != 0)
        {
            var a = filtered / scale; var b = _filter1 / scale; var c = _filter2 / scale;
            result = (a + b + c) / Math.Sqrt(3 * (a * a + b * b + c * c));
        }
        if (final)
        {
            _price = value; _change = change; _high = high;
            _filter2 = _filter1; _filter1 = filtered; _hasPrice = true;
        }
        return result;
    }
    internal void Reset() { _price = _change = _high = _filter1 = _filter2 = 0; _hasPrice = false; }
}
