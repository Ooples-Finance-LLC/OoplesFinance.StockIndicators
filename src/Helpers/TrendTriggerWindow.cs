using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TrendTriggerWindow : IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly PooledRingBuffer<double> _highest, _lowest;
    internal TrendTriggerWindow(int length)
    {
        _length = Math.Max(1, length); _high = new(_length); _low = new(_length);
        _highest = new(_length); _lowest = new(_length);
    }
    internal double Next(double high, double low, bool commit)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var oldHigh = _highest.Count == _length ? _highest[0] : 0;
        var oldLow = _lowest.Count == _length ? _lowest[0] : 0;
        var numerator = new ExactMeanAccumulator(); numerator.Add(highest, 200); numerator.Add(oldLow, -200); numerator.Add(oldHigh, -200); numerator.Add(lowest, 200);
        var denominator = new ExactMeanAccumulator(); denominator.Add(highest); denominator.Add(oldLow, -1); denominator.Add(oldHigh); denominator.Add(lowest, -1);
        var result = numerator.Ratio(denominator);
        if (commit) { _highest.TryAdd(highest, out _); _lowest.TryAdd(lowest, out _); }
        return result;
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _highest.Clear(); _lowest.Clear(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _highest.Dispose(); _lowest.Dispose(); }
}
