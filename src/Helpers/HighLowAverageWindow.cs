using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HighLowAverageWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly RocBankAverage? _upper, _lower;
    private readonly IMovingAverageSmoother? _upperFallback, _lowerFallback;
    // A finite bound is valid only when the caller will publish no more than that many inputs.
    internal HighLowAverageWindow(MovingAvgType kind, int length, int maxInputCount = int.MaxValue)
    {
        length = Math.Max(1, length); var capacity = Math.Min(length, Math.Max(1, maxInputCount));
        _high = new(capacity); _low = new(capacity);
        if (StrengthWindow.Supports(kind)) { _upper = new(kind, length, capacity); _lower = new(kind, length, capacity); }
        else { _upperFallback = MovingAverageSmootherFactory.Create(kind, length); _lowerFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal static double Midpoint(double upper, double lower)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(upper); sum.Add(lower); return sum.Mean(2);
    }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, bool commit)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var upper = _upper is null ? _upperFallback!.Next(highest, commit) : _upper.Next(new RocBankValue(highest), commit).Publish();
        var lower = _lower is null ? _lowerFallback!.Next(lowest, commit) : _lower.Next(new RocBankValue(lowest), commit).Publish();
        return (upper, Midpoint(upper, lower), lower);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _upper?.Reset(); _lower?.Reset(); _upperFallback?.Reset(); _lowerFallback?.Reset(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _upper?.Dispose(); _lower?.Dispose(); _upperFallback?.Dispose(); _lowerFallback?.Dispose(); }
}
