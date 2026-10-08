namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class InstantaneousTrendWindow
{
    private readonly RocBankValue _current, _previous, _older, _feedback, _decay;
    private RocBankValue _price1, _price2, _line1, _line2;
    private int _startup;
    internal InstantaneousTrendWindow(double alpha)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        var a = new RocBankValue(alpha); var square = a.Multiply(alpha); var retention = new RocBankValue(1 - alpha);
        _current = Add(a, square.Multiply(.25), -1); _previous = square.Multiply(.5); _older = Add(square.Multiply(.75), a, -1);
        _feedback = retention.Multiply(2); _decay = retention.Multiply(1 - alpha);
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue a, RocBankValue b, int sign = 1)
    { var product = new ExactMeanAccumulator(); product.AddProduct(a.Mantissa, -sign * b.Mantissa); product.ScaleByPowerOfTwo(a.UpperShift + b.UpperShift); sum.Subtract(product); }
    internal (double Line, double Signal) Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var sum = new ExactMeanAccumulator();
        RocBankValue line;
        if (_startup < 7)
        { current.AddTo(ref sum); _price1.AddTo(ref sum, 2); _price2.AddTo(ref sum); line = RocBankValue.Round(sum, count: 4); }
        else
        {
            Product(ref sum, current, _current); Product(ref sum, _price1, _previous); Product(ref sum, _price2, _older);
            Product(ref sum, _line1, _feedback); Product(ref sum, _line2, _decay, -1); line = RocBankValue.Round(sum);
        }
        var signal = new ExactMeanAccumulator(); line.AddTo(ref signal, 2); _line2.AddTo(ref signal, -1);
        if (commit) { _price2 = _price1; _price1 = current; _line2 = _line1; _line1 = line; if (_startup < 7) _startup++; }
        return (line.Publish(), signal.Mean(1));
    }
    internal void Reset() { _price1 = _price2 = _line1 = _line2 = default; _startup = 0; }
}
