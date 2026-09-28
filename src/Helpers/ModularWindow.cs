namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ModularWindow
{
    private readonly double _alpha, _beta; private RocBankValue _upper, _lower; private int _side; private bool _seeded;
    internal ModularWindow(int length, double beta)
    { if (double.IsNaN(beta) || double.IsInfinity(beta)) throw new ArgumentOutOfRangeException(nameof(beta)); _alpha = 2d / (Math.Max(1, length) + 1d); _beta = beta; }
    private static RocBankValue Add(RocBankValue a, RocBankValue b)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum); return RocBankValue.Round(sum); }
    private static int Compare(RocBankValue a, RocBankValue b)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return sum.Sign; }
    private static RocBankValue Blend(RocBankValue a, RocBankValue b, double gain) => Add(a.Multiply(gain), b.Multiply(1 - gain));
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var upper = Blend(current, _seeded ? _upper : current, _alpha); var lower = Blend(current, _seeded ? _lower : current, _alpha);
        if (Compare(current, upper) > 0) upper = current; if (Compare(current, lower) < 0) lower = current;
        var side = Compare(current, upper) == 0 ? 1 : Compare(current, lower) == 0 ? 0 : _side;
        var result = side == 1 ? Blend(upper, lower, _beta) : Blend(lower, upper, _beta);
        if (commit) { _upper = upper; _lower = lower; _side = side; _seeded = true; }
        return result.Publish();
    }
    internal void Reset() { _upper = _lower = default; _side = 0; _seeded = false; }
}
