using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class OpenCloseAverageWindow : IDisposable
{
    private readonly PooledRingBuffer<double>? _opens;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal OpenCloseAverageWindow(MovingAvgType kind, int length, int lag = 0)
    {
        if (lag > 0) _opens = new(lag);
        if (StrengthWindow.Supports(kind)) _average = new(kind, Math.Max(1, length), int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal static RocBankValue Difference(double open, double close)
    {
        var difference = new ExactMeanAccumulator(); difference.Add(close); difference.Add(open, -1);
        return RocBankValue.Round(difference);
    }
    internal (double Line, double Signal, double Histogram) Next(double open, double close, bool commit, double? customer = null)
    {
        var origin = _opens is null ? open : _opens.Count == _opens.Capacity ? _opens[0] : 0;
        var line = Difference(origin, close);
        var signal = customer.HasValue ? new RocBankValue(customer.Value) : _average is null
            ? new RocBankValue(_fallback!.Next(line.Publish(), commit)) : _average.Next(line, commit);
        var histogram = new ExactMeanAccumulator(); line.AddTo(ref histogram); signal.AddTo(ref histogram, -1);
        if (commit) _opens?.TryAdd(open, out _);
        return (line.Publish(), signal.Publish(), histogram.Mean(1));
    }
    internal void Reset() { _opens?.Clear(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _opens?.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
