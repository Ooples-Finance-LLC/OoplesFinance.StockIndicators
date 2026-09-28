namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ImpulseReactionWindow
{
    private readonly int _lag; private readonly RocBankValue _c1, _c2, _c3;
    private readonly Queue<double> _prices = new(); private RocBankValue _previous1, _previous2;
    internal ImpulseReactionWindow(int length1, int length2, double q)
    {
        if (double.IsNaN(q) || double.IsInfinity(q)) throw new ArgumentOutOfRangeException(nameof(q));
        _lag = Math.Max(1, length1); length2 = Math.Max(1, length2);
        _c2 = new RocBankValue(q).Multiply(2).Multiply(Math.Cos(2 * Math.PI / length2));
        _c3 = new RocBankValue(-q).Multiply(q); _c1 = Add(new RocBankValue(1), _c3).Multiply(.5);
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Product(RocBankValue a, RocBankValue b)
    { var product = new ExactMeanAccumulator(); product.AddProduct(a.Mantissa, b.Mantissa); product.ScaleByPowerOfTwo(a.UpperShift + b.UpperShift); return RocBankValue.Round(product); }
    internal double Next(double price, bool commit)
    {
        var prior = _prices.Count == _lag ? _prices.Peek() : 0;
        var change = Add(new RocBankValue(price), new RocBankValue(prior), -1);
        var reaction = Add(Add(Product(_c1, change), Product(_c2, _previous1)), Product(_c3, _previous2));
        var numerator = new ExactMeanAccumulator(); reaction.Multiply(100).AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); denominator.Add(price);
        var result = price != 0 ? numerator.Ratio(denominator) : 0;
        if (commit) { if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); _previous2 = _previous1; _previous1 = reaction; }
        return result;
    }
    internal void Reset() { _prices.Clear(); _previous1 = _previous2 = default; }
}
