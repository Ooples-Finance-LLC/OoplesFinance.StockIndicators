using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;
/// <summary>Roofing transfer with DC and Nyquist zeros before recursive poles.</summary>
internal sealed class EhlersRoofingFilterV2Kernel
{
    private readonly double _pole, _gain, _c1, _c2, _c3;
    private double _price1, _price2, _price3;
    private RocBankValue _first, _second, _output1, _output2;
    internal EhlersRoofingFilterV2Kernel(int upper, int lower, bool original = false)
    {
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / Math.Max(1, upper), .99);
        _pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        _gain = original ? ((1 + _pole) / 2) * ((1 + _pole) / 2) : _pole * _pole / 4;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lower); var radius = Math.Exp(-lowAngle);
        _c2 = 2 * radius * Math.Cos(Math.Min(lowAngle, .99)); _c3 = -radius * radius;
        var gap = 2 * Math.Exp(-lowAngle / 2) * Math.Sinh(lowAngle / 2); var sine = ExactVarianceWindow.Units(Math.Sin(Math.Min(lowAngle, .99) / 2));
        var gain = new ExactMeanAccumulator(); gain.AddProduct(gap, gap);
        var curved = new ExactMeanAccumulator(); curved.Add(-4 * radius, sine * sine); curved.ScaleByPowerOfTwo(-2148); gain.Subtract(curved); _c1 = gain.Mean(1);
    }
    private static void Product(ref ExactMeanAccumulator total, RocBankValue value, double coefficient)
    { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift); total.Subtract(negative); }
    internal double Next(double value, bool isFinal)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(value); numerator.Add(_price1, -1); numerator.Add(_price2, -1); numerator.Add(_price3);
        var drive = RocBankValue.Round(numerator, count: 2);
        var firstSum = new ExactMeanAccumulator(); Product(ref firstSum, drive, _gain); Product(ref firstSum, _first, _pole); var first = RocBankValue.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.AddTo(ref secondSum); Product(ref secondSum, _second, _pole); var second = RocBankValue.Round(secondSum);
        var outputSum = new ExactMeanAccumulator(); Product(ref outputSum, second, _c1); Product(ref outputSum, _output1, _c2); Product(ref outputSum, _output2, _c3); var output = RocBankValue.Round(outputSum);
        if (isFinal) { _price3 = _price2; _price2 = _price1; _price1 = value; _first = first; _second = second; _output2 = _output1; _output1 = output; }
        return output.Publish();
    }
    internal void Reset() { _price1 = _price2 = _price3 = 0; _first = _second = _output1 = _output2 = default; }
}
