namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RecursiveMedianOscillatorWindow
{
    private readonly RecursiveMedianWindow _median;
    private readonly double _drive, _feedback, _decay;
    private double _previous, _older;
    private RocBankValue _first, _second;
    internal RecursiveMedianOscillatorWindow(int length1, int length2, int length3)
    {
        _median = new(length1, length2);
        var angle = MathHelper.MinOrMax(1 / Math.Sqrt(2) * 2 * Math.PI / Math.Max(1, length3), .99, .01);
        var alpha = (Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle);
        _drive = Math.Pow(1 - alpha / 2, 2); _feedback = 2 * (1 - alpha); _decay = Math.Pow(1 - alpha, 2);
    }
    internal double Next(double price, bool commit)
    {
        var median = _median.Next(price, commit);
        var difference = new ExactMeanAccumulator(); difference.Add(median); difference.Add(_previous, -2); difference.Add(_older);
        var sum = new ExactMeanAccumulator(); RocBankValue.Round(difference).Multiply(_drive).AddTo(ref sum);
        _first.Multiply(_feedback).AddTo(ref sum); _second.Multiply(_decay).AddTo(ref sum, -1);
        var result = RocBankValue.Round(sum);
        if (commit) { _older = _previous; _previous = median; _second = _first; _first = result; }
        return result.Publish();
    }
    internal void Reset() { _median.Reset(); _previous = _older = 0; _first = _second = default; }
}
