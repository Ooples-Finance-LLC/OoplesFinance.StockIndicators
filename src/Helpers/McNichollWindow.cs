using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class McNichollWindow : IDisposable
{
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    private readonly int _length;
    internal McNichollWindow(MovingAvgType kind, int length, bool initializeFallback = true)
    {
        length = Math.Max(2, length); _length = length;
        if (StrengthWindow.Supports(kind)) { _first = new(kind, length, int.MaxValue); _second = new(kind, length, int.MaxValue); }
        else if (initializeFallback) { _firstFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _secondFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal double Next(double price, bool commit, double? customerFirst = null, double? customerSecond = null)
    {
        var first = customerFirst.HasValue ? new RocBankValue(customerFirst.Value) : _first is null
            ? new RocBankValue(_firstFallback!.Next(price, commit)) : _first.Next(new RocBankValue(price), commit);
        var second = customerSecond.HasValue ? new RocBankValue(customerSecond.Value) : _second is null
            ? new RocBankValue(_secondFallback!.Next(first.Publish(), commit)) : _second.Next(first, commit);
        // With alpha = 2/(length+1), the exact correction is
        // (2*length*first - (length+1)*second)/(length-1).
        var result = new ExactMeanAccumulator(); first.AddTo(ref result, 2L * _length); second.AddTo(ref result, -(_length + 1L));
        return result.Mean(_length - 1L);
    }
    internal void Reset() { _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
