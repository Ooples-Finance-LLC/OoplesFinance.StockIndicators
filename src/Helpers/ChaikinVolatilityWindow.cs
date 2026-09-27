using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChaikinVolatilityWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly int _lag;
    private readonly Queue<RocBankValue> _history = new();
    internal ChaikinVolatilityWindow(MovingAvgType kind, int smooth, int lag, int capacityHint = int.MaxValue)
    {
        smooth = Math.Max(1, smooth); _lag = Math.Max(1, lag);
        if (StrengthWindow.Supports(kind)) _average = new(kind, smooth, capacityHint);
        else _fallback = MovingAverageSmootherFactory.Create(kind, smooth);
    }
    internal static RocBankValue Range(double high, double low)
    {
        var range = new ExactMeanAccumulator(); range.Add(high); range.Add(low, -1);
        return RocBankValue.Round(range);
    }
    internal double Finish(RocBankValue value, bool commit)
    {
        var previous = _history.Count == _lag ? _history.Peek() : default;
        var result = 0d;
        if (previous.Mantissa != 0)
        {
            var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator, 100); previous.AddTo(ref numerator, -100);
            var denominator = new ExactMeanAccumulator(); previous.AddTo(ref denominator);
            result = numerator.Ratio(denominator);
        }
        if (commit) { if (_history.Count == _lag) _history.Dequeue(); _history.Enqueue(value); }
        return result;
    }
    internal double Next(double high, double low, bool commit)
    {
        var range = Range(high, low);
        var average = _average is not null ? _average.Next(range, commit) : new RocBankValue(_fallback!.Next(range.Publish(), commit));
        return Finish(average, commit);
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _history.Clear(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); _history.Clear(); }
}
