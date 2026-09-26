using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class GeneralizedDoubleWindow : IDisposable
{
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    private readonly double _factor;
    internal GeneralizedDoubleWindow(MovingAvgType kind, int length, double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _factor = factor;
        if (StrengthWindow.Supports(kind)) { _first = new(kind, length, int.MaxValue); _second = new(kind, length, int.MaxValue); }
        else { _firstFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _secondFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal double Next(double price, bool commit, double? customerFirst = null, double? customerSecond = null)
    {
        var first = customerFirst.HasValue ? new RocBankValue(customerFirst.Value) : _first is null
            ? new RocBankValue(_firstFallback!.Next(price, commit)) : _first.Next(new RocBankValue(price), commit);
        var second = customerSecond.HasValue ? new RocBankValue(customerSecond.Value) : _second is null
            ? new RocBankValue(_secondFallback!.Next(first.Publish(), commit)) : _second.Next(first, commit);
        // Evaluate the affine combination once, without rounding away the unit
        // coefficient in (1 + factor) or overflowing its cancelling products.
        var result = new ExactMeanAccumulator(); first.AddTo(ref result);
        result.AddProduct(first.Publish(), _factor); result.AddProduct(second.Publish(), _factor, -1);
        return result.Mean(1);
    }
    internal void Reset() { _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
