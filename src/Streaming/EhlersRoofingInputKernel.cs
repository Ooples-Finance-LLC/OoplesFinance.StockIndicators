namespace OoplesFinance.StockIndicators.Streaming;

// Apply the roofing numerator's DC and Nyquist zeros before the repeated pole.
// Averaging two separately filtered alternating prices would subtract their large
// steady responses and erase the small transient being normalized downstream.
internal sealed class EhlersRoofingInputKernel
{
    private readonly double _pole, _gain;
    private double _price1, _price2, _price3;
    private RocBankValue _first, _second;
    internal EhlersRoofingInputKernel(int length)
    {
        var alpha = EhlersFirstOrderCoefficient.Alpha(Math.Max(1, length) * Math.Sqrt(2));
        _pole = 1 - alpha;
        _gain = Math.Pow(1 - alpha / 2, 2);
    }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(negative); }
    internal double Next(double value, bool final) => NextExtended(value, final).Publish();
    internal RocBankValue NextExtended(double value, bool final)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(value); numerator.Add(_price1, -1); numerator.Add(_price2, -1); numerator.Add(_price3);
        var drive = RocBankValue.Round(numerator, count: 2);
        var firstSum = new ExactMeanAccumulator(); Product(ref firstSum, drive, _gain); Product(ref firstSum, _first, _pole); var first = RocBankValue.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.AddTo(ref secondSum); Product(ref secondSum, _second, _pole); var second = RocBankValue.Round(secondSum);
        if (final) { _price3 = _price2; _price2 = _price1; _price1 = value; _first = first; _second = second; }
        return second;
    }
    internal void Reset() { _price1 = _price2 = _price3 = 0; _first = _second = default; }
}
