using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TradeVolumeTotal
{
    private readonly double _tick;
    private ExactMeanAccumulator _total;
    private double _previous;
    internal TradeVolumeTotal(double minTickValue = 0.5)
    {
        if (double.IsNaN(minTickValue) || double.IsInfinity(minTickValue)) throw new ArgumentOutOfRangeException(nameof(minTickValue));
        _tick = minTickValue;
    }
    internal RocBankValue Next(double price, double volume, bool commit)
    {
        var up = new ExactMeanAccumulator(); up.Add(price); up.Add(_previous, -1); up.Add(_tick, -1);
        var down = new ExactMeanAccumulator(); down.Add(price); down.Add(_previous, -1); down.Add(_tick);
        var total = _total;
        if (up.Sign > 0) total.Add(volume);
        else if (down.Sign < 0) total.Add(volume, -1);
        if (commit) { _total = total; _previous = price; }
        return RocBankValue.Round(total);
    }
    internal void Reset() { _total = default; _previous = 0; }
}

internal sealed class TradeVolumeWindow : IDisposable
{
    private readonly TradeVolumeTotal _total;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal TradeVolumeWindow(MovingAvgType kind, int length, double minTickValue = 0.5)
    {
        _total = new(minTickValue);
        if (StrengthWindow.Supports(kind)) _average = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal (double Line, double Signal) Next(double price, double volume, bool commit, double? customerSignal = null)
    {
        var line = _total.Next(price, volume, commit);
        var signal = customerSignal.HasValue ? new RocBankValue(customerSignal.Value) : _average is null
            ? new RocBankValue(_fallback!.Next(line.Publish(), commit)) : _average.Next(line, commit);
        return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _total.Reset(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
