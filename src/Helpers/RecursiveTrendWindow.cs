namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RecursiveTrendWindow
{
    private readonly double _alpha;
    private RocBankValue _accumulator, _previous;
    private bool _hasPrevious;
    internal RecursiveTrendWindow(int length) => _alpha = 2d / (Math.Max(1, length) + 1d);
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price);
        var priorAccumulator = _hasPrevious ? _accumulator : current;
        var previous = _hasPrevious ? _previous : current;
        var accumulator = Add(priorAccumulator.Multiply(1 - _alpha), current);
        var adjusted = Add(Add(current, accumulator), priorAccumulator, -1);
        var value = Add(previous.Multiply(1 - _alpha), adjusted.Multiply(_alpha));
        if (commit) { _accumulator = accumulator; _previous = value; _hasPrevious = true; }
        return value.Publish();
    }
    internal void Reset() { _accumulator = default; _previous = default; _hasPrevious = false; }
}
