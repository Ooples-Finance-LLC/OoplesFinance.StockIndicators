namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SimpleCycleIndicatorWindow
{
    private readonly RocBankValue _lead, _retention, _square; private int _startup;
    private RocBankValue _price1, _price2, _price3, _smooth1, _smooth2, _cycle1, _cycle2;
    internal SimpleCycleIndicatorWindow(double alpha)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        var lead = 1 - .5 * alpha; var retention = 1 - alpha; _lead = new RocBankValue(lead).Multiply(lead); _retention = new RocBankValue(retention).Multiply(2); _square = new RocBankValue(retention).Multiply(retention);
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Product(RocBankValue a, RocBankValue b)
    { var product = new ExactMeanAccumulator(); product.AddProduct(a.Mantissa, b.Mantissa); product.ScaleByPowerOfTwo(a.UpperShift + b.UpperShift); return RocBankValue.Round(product); }
    private static RocBankValue Difference(RocBankValue value, RocBankValue previous1, RocBankValue previous2) => Add(Add(value, previous1.Multiply(2), -1), previous2);
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var total = Add(Add(Add(current, _price1.Multiply(2)), _price2.Multiply(2)), _price3);
        var sum = new ExactMeanAccumulator(); total.AddTo(ref sum); var smooth = RocBankValue.Round(sum, count: 6);
        var cycle = Add(Add(Product(_lead, Difference(smooth, _smooth1, _smooth2)), Product(_retention, _cycle1)), Product(_square, _cycle2), -1);
        var result = (_startup < 7 ? Difference(current, _price1, _price2).Multiply(.25) : cycle).Publish();
        if (commit) { _price3 = _price2; _price2 = _price1; _price1 = current; _smooth2 = _smooth1; _smooth1 = smooth; _cycle2 = _cycle1; _cycle1 = cycle; if (_startup < 7) _startup++; }
        return result;
    }
    internal void Reset() { _price1 = _price2 = _price3 = _smooth1 = _smooth2 = _cycle1 = _cycle2 = default; _startup = 0; }
}
