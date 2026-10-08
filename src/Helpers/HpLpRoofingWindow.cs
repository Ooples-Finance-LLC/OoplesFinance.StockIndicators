namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HpLpRoofingWindow
{
    private readonly double _gain, _retention, _c1, _c2, _c3; private double _price; private bool _seeded;
    private RocBankValue _high, _roof1, _roof2, _zero;
    internal HpLpRoofingWindow(int length1, int length2)
    {
        var argument = Math.Min(2 * Math.PI / Math.Max(1, length1), .99); var cosine = Math.Cos(argument);
        var alpha = cosine != 0 ? (cosine + Math.Sin(argument) - 1) / cosine : 0; _gain = 1 - alpha / 2; _retention = 1 - alpha;
        var angle = MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2); var decay = Math.Exp(-angle);
        _c2 = 2 * decay * Math.Cos(Math.Min(angle, .99)); _c3 = -decay * decay; _c1 = 1 - _c2 - _c3;
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal (double Roof, double Zero) Next(double price, bool commit)
    {
        var change = _seeded ? Add(new RocBankValue(price), new RocBankValue(_price), -1) : default;
        var high = Add(change.Multiply(_gain), _high.Multiply(_retention));
        var roof = Add(Add(Add(high, _high).Multiply(.5).Multiply(_c1), _roof1.Multiply(_c2)), _roof2.Multiply(_c3));
        var zero = Add(Add(roof, _roof1, -1).Multiply(_gain), _zero.Multiply(_retention));
        if (commit) { _price = price; _high = high; _roof2 = _roof1; _roof1 = roof; _zero = zero; _seeded = true; }
        return (roof.Publish(), zero.Publish());
    }
    internal void Reset() { _price = 0; _high = _roof1 = _roof2 = _zero = default; _seeded = false; }
}
