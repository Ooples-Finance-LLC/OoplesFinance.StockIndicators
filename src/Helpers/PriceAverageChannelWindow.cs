using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PriceAverageChannelWindow : IDisposable
{
    private readonly RocBankAverage? _upper, _lower;
    private readonly IMovingAverageSmoother? _upperFallback, _lowerFallback;
    internal PriceAverageChannelWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) { _upper = new(kind, length, int.MaxValue); _lower = new(kind, length, int.MaxValue); }
        else { _upperFallback = MovingAverageSmootherFactory.Create(kind, length); _lowerFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, bool commit)
    {
        var upper = _upper is null ? _upperFallback!.Next(high, commit) : _upper.Next(new RocBankValue(high), commit).Publish();
        var lower = _lower is null ? _lowerFallback!.Next(low, commit) : _lower.Next(new RocBankValue(low), commit).Publish();
        return (upper, HighLowAverageWindow.Midpoint(upper, lower), lower);
    }
    internal void Reset() { _upper?.Reset(); _lower?.Reset(); _upperFallback?.Reset(); _lowerFallback?.Reset(); }
    public void Dispose() { _upper?.Dispose(); _lower?.Dispose(); _upperFallback?.Dispose(); _lowerFallback?.Dispose(); }
}
