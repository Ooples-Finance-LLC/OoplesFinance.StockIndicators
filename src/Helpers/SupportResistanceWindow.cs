using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SupportResistanceWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal SupportResistanceWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal static double Lower(double middle, double factor)
    {
        var top = new ExactMeanAccumulator(); top.Add(middle, 100);
        var bottom = new ExactMeanAccumulator(); bottom.Add(100); bottom.Add(factor);
        return bottom.IsExactlyZero ? 0 : top.Ratio(bottom);
    }
    internal double Next(double price, bool commit) => _average is null ? _fallback!.Next(price, commit) : _average.Next(new RocBankValue(price), commit).Publish();
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
