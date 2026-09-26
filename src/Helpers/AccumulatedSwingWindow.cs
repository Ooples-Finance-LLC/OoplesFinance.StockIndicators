using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AccumulatedSwingWindow : IDisposable
{
    private readonly double _limitMove;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    private RocBankValue _previous;
    private double _open, _close;
    private bool _started;
    internal AccumulatedSwingWindow(MovingAvgType kind, int length, double limitMove, int capacityHint = int.MaxValue)
    {
        _limitMove = limitMove;
        if (StrengthWindow.Supports(kind)) _signal = new(kind, length, capacityHint);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal (double Value, double Signal) Next(double open, double high, double low, double close, bool commit)
    {
        var swing = _started ? WilderSwingIndex.ComputeExtended(open, high, low, close, _open, _close, _limitMove) : default;
        var sum = new ExactMeanAccumulator(); _previous.AddTo(ref sum); swing.AddTo(ref sum);
        var accumulated = RocBankValue.Round(sum);
        var signal = _signal is not null ? _signal.Next(accumulated, commit).Publish() : _fallback!.Next(accumulated.Publish(), commit);
        if (commit) { _previous = accumulated; _open = open; _close = close; _started = true; }
        return (accumulated.Publish(), signal);
    }
    internal void Reset() { _previous = default; _open = _close = 0; _started = false; _signal?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _signal?.Dispose(); _fallback?.Dispose(); }
}
