using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveZoneWindow : IDisposable
{
    private readonly RocBankAverage[]? _exact;
    private readonly IMovingAverageSmoother[]? _fallback;
    private readonly double _pct;
    internal static int Period(int length) => Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, length)))));
    internal AdaptiveZoneWindow(MovingAvgType kind, int length, double pct, int capacityHint = int.MaxValue)
    {
        _pct = pct; var period = Period(length);
        if (StrengthWindow.Supports(kind)) _exact = Enumerable.Range(0, 4).Select(_ => new RocBankAverage(kind, period, capacityHint)).ToArray();
        else _fallback = Enumerable.Range(0, 4).Select(_ => MovingAverageSmootherFactory.Create(kind, period)).ToArray();
    }
    private RocBankValue Stage(int slot, RocBankValue value, bool commit) => _exact is not null ? _exact[slot].Next(value, commit) : new RocBankValue(_fallback![slot].Next(value.Publish(), commit));
    internal static (double Upper, double Middle, double Lower) Bands(RocBankValue center, RocBankValue width, double pct)
    {
        var distance = width.Multiply(pct); var upper = new ExactMeanAccumulator(); center.AddTo(ref upper); distance.AddTo(ref upper);
        var lower = new ExactMeanAccumulator(); center.AddTo(ref lower); distance.AddTo(ref lower, -1);
        return (upper.Mean(1), center.Publish(), lower.Mean(1));
    }
    internal (double Upper, double Middle, double Lower) Next(double price, double high, double low, bool commit)
    {
        var center = Stage(1, Stage(0, new RocBankValue(price), commit), commit);
        var difference = new ExactMeanAccumulator(); difference.Add(high); difference.Add(low, -1);
        var width = Stage(3, Stage(2, RocBankValue.Round(difference), commit), commit);
        return Bands(center, width, _pct);
    }
    internal void Reset() { if (_exact is not null) foreach (var stage in _exact) stage.Reset(); if (_fallback is not null) foreach (var stage in _fallback) stage.Reset(); }
    public void Dispose() { if (_exact is not null) foreach (var stage in _exact) stage.Dispose(); if (_fallback is not null) foreach (var stage in _fallback) stage.Dispose(); }
}
