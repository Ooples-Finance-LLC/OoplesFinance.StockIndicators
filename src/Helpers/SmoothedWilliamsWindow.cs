using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SmoothedWilliamsWindow
{
    private readonly int _length;
    private readonly double _gain;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private long _count;
    private RocBankValue _previous;
    internal SmoothedWilliamsWindow(int length, int smoothing) { _length = Math.Max(1, length); _gain = 2d / (Math.Max(1, smoothing) + 1L); }
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    internal double Next(double high, double low, double close, bool commit)
    {
        var max = _high.First; while (max is not null && max.Value.Index <= _count - _length) max = max.Next;
        var min = _low.First; while (min is not null && min.Value.Index <= _count - _length) min = min.Next;
        var upper = max is null ? high : Math.Max(high, max.Value.Value); var lower = min is null ? low : Math.Min(low, min.Value.Value);
        var raw = new RocBankValue(-50);
        if (_count + 1 >= _length && upper > lower)
        {
            var h = ExactVarianceWindow.Units(upper); var l = ExactVarianceWindow.Units(lower); var c = ExactVarianceWindow.Units(close);
            var units = RocBankValue.RoundUnits((100 * (c - h)) << 1074, h - l);
            var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); raw = RocBankValue.Round(sum);
        }
        var result = raw;
        if (_count > 0)
        {
            var sum = new ExactMeanAccumulator(); raw.Multiply(_gain).AddTo(ref sum); _previous.Multiply(1 - _gain).AddTo(ref sum); result = RocBankValue.Round(sum);
        }
        if (commit) { Commit(_high, high, true); Commit(_low, low, false); _previous = result; _count++; }
        return result.Publish();
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _previous = default; _count = 0; }
}
