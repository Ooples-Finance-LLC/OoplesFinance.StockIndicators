using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DailyDeltaWindow : IDisposable
{
    private readonly RocBankAverage? _high, _low;
    private readonly IMovingAverageSmoother? _highFallback, _lowFallback;
    internal DailyDeltaWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) { _high = new(kind, length, capacityHint); _low = new(kind, length, capacityHint); }
        else { _highFallback = MovingAverageSmootherFactory.Create(kind, length); _lowFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal static (double Upper, double Lower) Finish(double high, double low, double averageHigh, double averageLow)
    {
        var difference = new ExactMeanAccumulator(); difference.Add(averageHigh); difference.Add(averageLow, -1);
        var width = RocBankValue.Round(difference);
        var upper = new ExactMeanAccumulator(); upper.Add(high); width.AddTo(ref upper);
        var lower = new ExactMeanAccumulator(); lower.Add(low); width.AddTo(ref lower, -1);
        return (upper.Mean(1), lower.Mean(1));
    }
    internal (double Upper, double Lower) Next(double high, double low, bool commit)
    {
        var averageHigh = _high is not null ? _high.Next(new RocBankValue(high), commit).Publish() : _highFallback!.Next(high, commit);
        var averageLow = _low is not null ? _low.Next(new RocBankValue(low), commit).Publish() : _lowFallback!.Next(low, commit);
        return Finish(high, low, averageHigh, averageLow);
    }
    internal void Reset() { _high?.Reset(); _low?.Reset(); _highFallback?.Reset(); _lowFallback?.Reset(); }
    public void Dispose() { _high?.Dispose(); _low?.Dispose(); _highFallback?.Dispose(); _lowFallback?.Dispose(); }
}
