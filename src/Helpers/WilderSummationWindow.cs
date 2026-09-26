namespace OoplesFinance.StockIndicators.Helpers;

// Keep unpublished recurrence stages recoverable when the sum temporarily exceeds binary64.
internal sealed class WilderSummationWindow
{
    private readonly int _length;
    private RocBankValue _previous;
    internal WilderSummationWindow(int length) => _length = Math.Max(1, length);
    internal double Next(double price, bool commit)
    {
        var previous = new ExactMeanAccumulator(); _previous.AddTo(ref previous);
        var decay = RocBankValue.Round(previous, count: _length);
        decay.AddTo(ref previous, -1);
        var retained = RocBankValue.Round(previous);
        var total = new ExactMeanAccumulator(); retained.AddTo(ref total); total.Add(price);
        var result = RocBankValue.Round(total);
        if (commit) _previous = result;
        return result.Publish();
    }
    internal void Reset() => _previous = default;
}
