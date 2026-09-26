using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AlligatorLineWindow : IDisposable
{
    private readonly int _offset;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly Queue<double> _history = new();
    internal AlligatorLineWindow(MovingAvgType kind, int length, int offset)
    {
        _offset = Math.Max(0, offset); length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal double Next(double price, bool commit)
    {
        var average = _average is null ? _fallback!.Next(price, commit) : _average.Next(new RocBankValue(price), commit).Publish();
        var value = _offset == 0 ? average : _history.Count < _offset ? 0 : _history.Peek();
        if (commit && _offset > 0) { if (_history.Count == _offset) _history.Dequeue(); _history.Enqueue(average); }
        return value;
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _history.Clear(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
