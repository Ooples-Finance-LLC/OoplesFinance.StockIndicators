namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ReverseEmaWindow
{
    private readonly double _alpha, _complement; private readonly double[] _coefficients = new double[8];
    private readonly RocBankValue[] _history = new RocBankValue[8];
    internal ReverseEmaWindow(double alpha)
    {
        if (double.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        _alpha = Math.Max(.01, Math.Min(.99, alpha)); _complement = 1 - _alpha;
        for (var stage = 0; stage < 8; stage++) _coefficients[stage] = Math.Pow(_complement, 1 << stage);
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal double Next(double price, bool commit)
    {
        Span<RocBankValue> next = stackalloc RocBankValue[8];
        var ema = Add(new RocBankValue(price).Multiply(_alpha), _history[0].Multiply(_complement)); next[0] = ema; var reverse = ema;
        for (var stage = 0; stage < 8; stage++)
        {
            reverse = Add(reverse.Multiply(_coefficients[stage]), _history[stage]);
            if (stage < 7) next[stage + 1] = reverse;
        }
        var result = Add(ema, reverse.Multiply(_alpha), -1).Publish();
        if (commit) next.CopyTo(_history);
        return result;
    }
    internal void Reset() => Array.Clear(_history, 0, _history.Length);
}
