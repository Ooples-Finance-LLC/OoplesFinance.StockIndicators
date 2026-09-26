using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HighLowBandsWindow : IDisposable
{
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    internal HighLowBandsWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) { _first = new(kind, length, int.MaxValue); _second = new(kind, length, int.MaxValue); }
        else { _firstFallback = MovingAverageSmootherFactory.Create(kind, length); _secondFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal static void ValidateShift(double shift)
    {
        if (double.IsNaN(shift) || double.IsInfinity(shift)) throw new ArgumentOutOfRangeException(nameof(shift));
    }
    internal static double Shift(double middle, double percent)
    {
        var total = new ExactMeanAccumulator(); total.Add(middle, 100); total.AddProduct(middle, percent);
        return total.Mean(100);
    }
    internal double Next(double price, bool commit)
    {
        var first = _first is null ? _firstFallback!.Next(price, commit) : _first.Next(new RocBankValue(price), commit).Publish();
        return _second is null ? _secondFallback!.Next(first, commit) : _second.Next(new RocBankValue(first), commit).Publish();
    }
    internal void Reset() { _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
