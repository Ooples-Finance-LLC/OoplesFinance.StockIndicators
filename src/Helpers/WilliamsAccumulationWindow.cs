namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WilliamsAccumulationWindow
{
    private ExactMeanAccumulator _total;
    private double _previous;
    private bool _hasPrevious;
    internal RocBankValue Next(double high, double low, double close, bool commit)
    {
        var total = _total;
        if (_hasPrevious && close != _previous)
        {
            var endpoint = close > _previous ? Math.Min(low, _previous) : Math.Max(high, _previous);
            total.Add(close); total.Add(endpoint, -1);
        }
        var result = RocBankValue.Round(total);
        if (commit) { _total = total; _previous = close; _hasPrevious = true; }
        return result;
    }
    internal void Reset() { _total = default; _previous = 0; _hasPrevious = false; }
}
