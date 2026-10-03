using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class OneBarReturnWindow : IDisposable
{
    private readonly bool _cumulative;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    private double _previous;
    private bool _hasPrevious;
    private RocBankValue _last;
    internal OneBarReturnWindow(bool cumulative, MovingAvgType kind, int length, bool external = false, int capacityHint = int.MaxValue)
    {
        _cumulative = cumulative; if (!external) { if (StrengthWindow.Supports(kind)) _signal = new(kind, Math.Max(1, length), capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal (double Value, double Signal) Next(double close, bool commit)
    {
        RocBankValue value = default; var denominator = _cumulative ? _previous : close;
        if (_hasPrevious && denominator != 0)
        {
            var numerator = new ExactMeanAccumulator(); numerator.Add(close, _cumulative ? 1 : 100); numerator.Add(_previous, _cumulative ? -1 : -100); value = RocBankValue.Round(numerator, denominator);
            if (_cumulative) { var total = new ExactMeanAccumulator(); _last.AddTo(ref total); value.AddTo(ref total); value = RocBankValue.Round(total); }
        }
        var signal = _signal is not null ? _signal.Next(value, commit).Publish() : _fallback?.Next(value.Publish(), commit) ?? 0;
        if (commit) { _previous = close; _last = value; _hasPrevious = true; }
        return (value.Publish(), signal);
    }
    internal void Reset() { _previous = 0; _last = default; _hasPrevious = false; _signal?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _signal?.Dispose(); _fallback?.Dispose(); }
}
