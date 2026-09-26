using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class OpenCloseHistogramWindow : IDisposable
{
    private readonly RocBankAverage? _open, _close;
    private readonly IMovingAverageSmoother? _openFallback, _closeFallback;
    internal OpenCloseHistogramWindow(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) { _open = new(kind, length, int.MaxValue); _close = new(kind, length, int.MaxValue); }
        else { _openFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _closeFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal double Next(double open, double close, bool commit, double? customerOpen = null, double? customerClose = null)
    {
        var first = customerOpen.HasValue ? new RocBankValue(customerOpen.Value) : _open is null
            ? new RocBankValue(_openFallback!.Next(open, commit)) : _open.Next(new RocBankValue(open), commit);
        var second = customerClose.HasValue ? new RocBankValue(customerClose.Value) : _close is null
            ? new RocBankValue(_closeFallback!.Next(close, commit)) : _close.Next(new RocBankValue(close), commit);
        var difference = new ExactMeanAccumulator(); second.AddTo(ref difference); first.AddTo(ref difference, -1);
        return difference.Mean(1);
    }
    internal void Reset() { _open?.Reset(); _close?.Reset(); _openFallback?.Reset(); _closeFallback?.Reset(); }
    public void Dispose() { _open?.Dispose(); _close?.Dispose(); _openFallback?.Dispose(); _closeFallback?.Dispose(); }
}
