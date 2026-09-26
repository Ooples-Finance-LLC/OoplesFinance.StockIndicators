namespace OoplesFinance.StockIndicators.Streaming;

// Apply the roofing numerator's DC and Nyquist zeros before the repeated pole.
// Averaging two separately filtered alternating prices would subtract their large
// steady responses and erase the small transient being normalized downstream.
internal sealed class EhlersRoofingInputKernel
{
    private readonly double _pole, _gain;
    private double _price1, _price2, _price3, _first, _second;
    internal EhlersRoofingInputKernel(int length)
    {
        var alpha = EhlersFirstOrderCoefficient.Alpha(Math.Max(1, length) * Math.Sqrt(2));
        _pole = 1 - alpha;
        _gain = Math.Pow(1 - alpha / 2, 2);
    }
    internal double Next(double value, bool final)
    {
        var drive = ((value - _price1) - (_price2 - _price3)) / 2;
        var first = _gain * drive + _pole * _first;
        var second = first + _pole * _second;
        if (final)
        {
            _price3 = _price2; _price2 = _price1; _price1 = value;
            _first = first; _second = second;
        }
        return second;
    }
    internal void Reset() => _price1 = _price2 = _price3 = _first = _second = 0;
}
