using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TurboTriggerWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallback;
    internal TurboTriggerWindow(MovingAvgType kind, int length, int smoothing)
    {
        var periods = new[] { smoothing, smoothing, smoothing, smoothing, length, length, length, length };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(n => new RocBankAverage(kind, Math.Max(1, n), int.MaxValue)).ToArray();
        else _fallback = periods.Select(n => MovingAverageSmootherFactory.Create(kind, Math.Max(1, n))).ToArray();
    }
    private RocBankValue Stage(int slot, RocBankValue value, bool commit) => _averages is not null ? _averages[slot].Next(value, commit) : new(_fallback![slot].Next(value.Publish(), commit));
    private static RocBankValue Combine(RocBankValue a, RocBankValue b, int sign, int divisor = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum, count: divisor); }
    internal (double Bull, double Trigger) Next(double close, double open, double high, double low, bool commit)
    {
        var c = Stage(0, new(close), commit); var o = Stage(1, new(open), commit);
        var h = Stage(2, new(high), commit); var l = Stage(3, new(low), commit);
        var center = Stage(4, Combine(c, o, 1, 2), commit);
        var bull = Stage(5, Combine(h, center, -1), commit); var bear = Stage(6, Combine(center, l, -1), commit);
        var trigger = Stage(7, Combine(bull, bear, -1), commit);
        return (bull.Publish(), trigger.Publish());
    }
    internal void Reset() { if (_averages is not null) foreach (var v in _averages) v.Reset(); if (_fallback is not null) foreach (var v in _fallback) v.Reset(); }
    public void Dispose() { if (_averages is not null) foreach (var v in _averages) v.Dispose(); if (_fallback is not null) foreach (var v in _fallback) v.Dispose(); }
}
