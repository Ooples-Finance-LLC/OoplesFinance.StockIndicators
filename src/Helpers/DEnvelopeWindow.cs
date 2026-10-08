namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DEnvelopeWindow
{
    private readonly int _length;
    private readonly double _factor;
    private RocBankValue _first, _second, _firstDeviation, _secondDeviation;
    private double _previousClose;
    internal DEnvelopeWindow(int length, double factor) { _length = Math.Max(1, length); _factor = factor; }
    private RocBankValue Smooth(RocBankValue value, RocBankValue previous)
    {
        var sum = new ExactMeanAccumulator(); value.AddTo(ref sum, 2); previous.AddTo(ref sum, _length - 1L);
        return RocBankValue.Round(sum, count: _length + 1L);
    }
    private RocBankValue Correct(RocBankValue first, RocBankValue second)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum, 2L * _length); second.AddTo(ref sum, -(_length + 1L));
        return RocBankValue.Round(sum, count: _length - 1L);
    }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var first = Smooth(new RocBankValue(close), _first); var second = Smooth(first, _second);
        var fallback = new ExactMeanAccumulator(); fallback.Add(close, 2); fallback.Add(_previousClose, -1);
        var center = _length == 1 ? RocBankValue.Round(fallback) : Correct(first, second);
        var distance = new ExactMeanAccumulator(); distance.Add(close); center.AddTo(ref distance, -1); var deviation = RocBankValue.Round(distance);
        if (deviation.Mantissa < 0) deviation = new(-deviation.Mantissa, deviation.UpperShift);
        var firstDeviation = Smooth(deviation, _firstDeviation); var secondDeviation = Smooth(firstDeviation, _secondDeviation);
        var widthFallback = new ExactMeanAccumulator(); deviation.AddTo(ref widthFallback, 2); _firstDeviation.AddTo(ref widthFallback, -1);
        var width = _length == 1 ? RocBankValue.Round(widthFallback) : Correct(firstDeviation, secondDeviation);
        if (width.Mantissa < 0) width = default;
        if (commit) { _first = first; _second = second; _firstDeviation = firstDeviation; _secondDeviation = secondDeviation; _previousClose = close; }
        return KeltnerWindow.Bands(center, width, _factor);
    }
    internal void Reset() { _first = _second = _firstDeviation = _secondDeviation = default; _previousClose = 0; }
}
