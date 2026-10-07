namespace OoplesFinance.StockIndicators.Helpers;
// (fastGain-slowGain)*(1-z^-1) / ((1-fastPole*z^-1)*(1-slowPole*z^-1)).
// Apply the DC zero before the poles, retaining unpublished extended values.
internal sealed class SuperPassbandWindow
{
    private readonly double _gain, _fastPole, _slowPole;
    private readonly int _length;
    private double _price;
    private RocBankValue _first, _second;
    private readonly Queue<ExactMeanAccumulator> _history = new();
    private ExactMeanAccumulator _squares;
    internal SuperPassbandWindow(int fast, int slow, int numerator, int length)
    {
        numerator = Math.Max(1, numerator); _length = Math.Max(1, length);
        var a = Math.Max(.01, Math.Min(.99, (double)numerator / Math.Max(1, fast)));
        var b = Math.Max(.01, Math.Min(.99, (double)numerator / Math.Max(1, slow)));
        _gain = a - b; _fastPole = 1 - a; _slowPole = 1 - b;
    }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(negative); }
    internal (double Line, double Upper, double Lower) Next(double price, bool commit)
    {
        var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(_price, -1); var drive = RocBankValue.Round(difference);
        var firstSum = new ExactMeanAccumulator(); Product(ref firstSum, drive, _gain); Product(ref firstSum, _first, _fastPole); var first = RocBankValue.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.AddTo(ref secondSum); Product(ref secondSum, _second, _slowPole); var second = RocBankValue.Round(secondSum);
        var square = new ExactMeanAccumulator(); square.AddProduct(second.Mantissa, second.Mantissa); square.ScaleByPowerOfTwo(2 * second.UpperShift);
        var total = _squares; if (_history.Count == _length) total.Subtract(_history.Peek());
        var negative = new ExactMeanAccumulator(); negative.Subtract(square); total.Subtract(negative);
        var rms = total.SqrtMean(Math.Min((long)_length, _history.Count + 1L));
        if (commit) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(square); _squares = total; _price = price; _first = first; _second = second; }
        return (second.Publish(), rms, -rms);
    }
    internal void Reset() { _history.Clear(); _squares = default; _first = _second = default; _price = 0; }
}
