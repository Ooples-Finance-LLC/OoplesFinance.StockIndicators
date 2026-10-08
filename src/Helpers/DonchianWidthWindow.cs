using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DonchianWidthWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly RocBankAverage? _signal, _center;
    private readonly IMovingAverageSmoother? _signalFallback, _centerFallback;
    internal DonchianWidthWindow(MovingAvgType kind, int length, int smoothLength, bool external = false, int capacityHint = int.MaxValue)
    {
        _high = new(Math.Max(1, length)); _low = new(Math.Max(1, length));
        if (!external)
        {
            if (StrengthWindow.Supports(kind)) { _center = new(kind, length, capacityHint); _signal = new(kind, smoothLength, capacityHint); }
            else { _centerFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _signalFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, smoothLength)); }
        }
    }
    internal (double Width, double Signal, double Center) Next(double high, double low, double close, bool commit)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var range = new ExactMeanAccumulator(); range.Add(highest); range.Add(lowest, -1); var width = RocBankValue.Round(range);
        var signal = _signal is not null ? _signal.Next(width, commit).Publish() : _signalFallback?.Next(width.Publish(), commit) ?? 0;
        var center = _center is not null ? _center.Next(new RocBankValue(close), commit).Publish() : _centerFallback?.Next(close, commit) ?? 0;
        return (width.Publish(), signal, center);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _signal?.Reset(); _center?.Reset(); _signalFallback?.Reset(); _centerFallback?.Reset(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _signal?.Dispose(); _center?.Dispose(); _signalFallback?.Dispose(); _centerFallback?.Dispose(); }
}
