namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Equivalent roofing transfer with its DC and Nyquist zeros applied before recursive poles.</summary>
internal sealed class EhlersRoofingFilterV2Kernel
{
    private readonly double _pole, _gain, _c1, _c2, _c3;
    private double _price1, _price2, _price3, _first, _second, _output1, _output2;

    internal EhlersRoofingFilterV2Kernel(int upper, int lower)
    {
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / Math.Max(1, upper), .99);
        _pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        _gain = _pole * _pole / 4;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lower);
        var radius = Math.Exp(-lowAngle);
        _c2 = 2 * radius * Math.Cos(Math.Min(lowAngle, .99));
        _c3 = -radius * radius;
        _c1 = 1 - _c2 - _c3;
    }

    internal double Next(double value, bool isFinal)
    {
        // (1-z^-1)^2(1+z^-1)/2: exact cancellation for constant/ramp/Nyquist input.
        var drive = ((value - _price1) - (_price2 - _price3)) / 2;
        var first = _gain * drive + _pole * _first;
        var second = first + _pole * _second;
        var output = _c1 * second + _c2 * _output1 + _c3 * _output2;
        if (isFinal)
        {
            _price3 = _price2; _price2 = _price1; _price1 = value;
            _first = first; _second = second;
            _output2 = _output1; _output1 = output;
        }
        return output;
    }

    internal void Reset() => _price1 = _price2 = _price3 = _first = _second = _output1 = _output2 = 0;
}
