using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ForceWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private double _previous;
    private bool _hasPrevious;
    internal ForceWindow(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) _average = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal static RocBankValue RawForce(double price, double previous, double volume)
    {
        var value = new ExactMeanAccumulator(); value.AddProduct(price, volume); value.AddProduct(previous, volume, -1);
        return RocBankValue.Round(value);
    }
    internal double Next(double price, double volume, bool commit, double? customerMean = null)
    {
        var force = _hasPrevious ? RawForce(price, _previous, volume) : default;
        var mean = customerMean.HasValue ? new RocBankValue(customerMean.Value) : _average is null
            ? new RocBankValue(_fallback!.Next(force.Publish(), commit)) : _average.Next(force, commit);
        if (commit) { _previous = price; _hasPrevious = true; }
        return mean.Publish();
    }
    internal void Reset() { _previous = 0; _hasPrevious = false; _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
