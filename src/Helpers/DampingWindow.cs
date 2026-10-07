using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DampingWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly Queue<RocBankValue> _history = new();
    private RocBankValue _previous;
    internal DampingWindow(MovingAvgType kind, int length, int capacity = int.MaxValue)
    { if (StrengthWindow.Supports(kind)) _average = new(kind, Math.Max(1, length), capacity); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    internal double Next(double high, double low, bool commit, double? externalAverage = null)
    {
        var difference = new ExactMeanAccumulator(); difference.Add(high); difference.Add(low, -1); var range = RocBankValue.Round(difference);
        var average = externalAverage.HasValue ? new RocBankValue(externalAverage.Value) : _average is null ? new RocBankValue(_fallback!.Next(range.Publish(), commit)) : _average.Next(range, commit);
        var older = _history.Count == 6 ? _history.Peek() : default; var result = 0d;
        if (older.Mantissa != 0)
        { var numerator = new ExactMeanAccumulator(); _previous.AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); older.AddTo(ref denominator); result = numerator.Ratio(denominator); }
        if (commit) { if (_history.Count == 6) _history.Dequeue(); _history.Enqueue(average); _previous = average; }
        return result;
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _history.Clear(); _previous = default; }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
