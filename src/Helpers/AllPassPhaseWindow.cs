namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AllPassPhaseWindow
{
    private readonly RocBankValue _a2, _a3, _b2, _b3;
    private RocBankValue _price1, _price2, _phase1, _phase2;
    internal AllPassPhaseWindow(int length, double q)
    {
        if (double.IsNaN(q) || double.IsInfinity(q)) throw new ArgumentOutOfRangeException(nameof(q));
        var cosine = length != 0 ? Math.Cos(2 * Math.PI / length) : 0;
        _a2 = q != 0 && length != 0 ? Divide(new RocBankValue(-2 * cosine), q) : default;
        var reciprocal = q != 0 ? Divide(new RocBankValue(1), q) : default; _a3 = Product(reciprocal, reciprocal);
        _b2 = length != 0 ? new RocBankValue(q).Multiply(-2).Multiply(cosine) : default; _b3 = new RocBankValue(q).Multiply(q);
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Divide(RocBankValue value, double denominator)
    { var sum = new ExactMeanAccumulator(); value.AddTo(ref sum); return RocBankValue.Round(sum, denominator); }
    private static RocBankValue Product(RocBankValue a, RocBankValue b)
    { var product = new ExactMeanAccumulator(); product.AddProduct(a.Mantissa, b.Mantissa); product.ScaleByPowerOfTwo(a.UpperShift + b.UpperShift); return RocBankValue.Round(product); }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price);
        var input = Add(Add(current, Product(_a2, _price1)), Product(_a3, _price2));
        var phase = Add(Add(Product(_b3, input), Product(_b2, _phase1), -1), Product(_b3, _phase2), -1);
        if (commit) { _price2 = _price1; _price1 = current; _phase2 = _phase1; _phase1 = phase; }
        return phase.Publish();
    }
    internal void Reset() { _price1 = _price2 = _phase1 = _phase2 = default; }
}
