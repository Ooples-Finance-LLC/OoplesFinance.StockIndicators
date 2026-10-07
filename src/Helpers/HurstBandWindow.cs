namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HurstBandWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly ExactPartialMeanWindow _mean;
    private readonly double _inner, _outer, _extreme;
    internal HurstBandWindow(int length, double inner, double outer, double extreme)
    {
        HighLowBandsWindow.ValidateShift(inner); HighLowBandsWindow.ValidateShift(outer); HighLowBandsWindow.ValidateShift(extreme);
        length = Math.Max(1, length); var delay = (int)Math.Min(530, Math.Max(2, (length + 1L) / 2 + 1));
        _prices = new(delay); _mean = new(length); _inner = inner; _outer = outer; _extreme = extreme;
    }
    internal (double Middle, double UpperInner, double LowerInner, double UpperOuter, double LowerOuter, double UpperExtreme, double LowerExtreme) Next(double close, bool commit)
    {
        var delayed = _prices.Count == _prices.Capacity ? _prices[0] : 0; var middle = _mean.Next(delayed, commit);
        if (commit) _prices.TryAdd(close, out _);
        return (middle, HighLowBandsWindow.Shift(middle, _inner), HighLowBandsWindow.Shift(middle, -_inner), HighLowBandsWindow.Shift(middle, _outer), HighLowBandsWindow.Shift(middle, -_outer), HighLowBandsWindow.Shift(middle, _extreme), HighLowBandsWindow.Shift(middle, -_extreme));
    }
    internal void Reset() { _prices.Clear(); _mean.Reset(); }
    public void Dispose() { _prices.Dispose(); _mean.Dispose(); }
}
