namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AverageErrorWindow
{
    private readonly double _c1, _c2, _c3; private int _startup;
    private RocBankValue _price, _smooth1, _smooth2, _error1, _error2;
    internal AverageErrorWindow(int length)
    {
        var angle = MathHelper.Sqrt2 * Math.PI / Math.Max(1, length);
        var decay = Math.Exp(Math.Max(-.99, Math.Min(-.01, -angle)));
        _c2 = 2 * decay * Math.Cos(Math.Max(.01, Math.Min(.99, angle))); _c3 = -1 * decay * decay; _c1 = 1 - _c2 - _c3;
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price);
        var smooth = _startup < 3 ? current : Add(Add(Add(current, _price).Multiply(.5 * _c1), _smooth1.Multiply(_c2)), _smooth2.Multiply(_c3));
        var error = _startup < 3 ? default : Add(Add(Add(current, smooth, -1).Multiply(_c1), _error1.Multiply(_c2)), _error2.Multiply(_c3));
        var result = Add(smooth, error).Publish();
        if (commit) { _price = current; _smooth2 = _smooth1; _smooth1 = smooth; _error2 = _error1; _error1 = error; if (_startup < 3) _startup++; }
        return result;
    }
    internal void Reset() { _price = _smooth1 = _smooth2 = _error1 = _error2 = default; _startup = 0; }
}
