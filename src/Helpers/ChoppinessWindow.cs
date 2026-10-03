using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChoppinessWindow
{
    private readonly int _length;
    private readonly Queue<BigInteger> _ranges = new();
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private BigInteger _sum;
    private long _count;
    private double _previous;
    internal ChoppinessWindow(int length) => _length = Math.Max(2, length);
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    internal double Next(double high, double low, double close, bool commit)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var previous = ExactVarianceWindow.Units(_count == 0 ? close : _previous);
        var tr = RocBankValue.RoundUnits(BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previous), BigInteger.Abs(l - previous))), BigInteger.One);
        var sum = _sum + tr - (_ranges.Count == _length ? _ranges.Peek() : BigInteger.Zero);
        var max = _high.First; while (max is not null && max.Value.Index <= _count - _length) max = max.Next;
        var min = _low.First; while (min is not null && min.Value.Index <= _count - _length) min = min.Next;
        var highest = max is null ? high : Math.Max(high, max.Value.Value);
        var lowest = min is null ? low : Math.Min(low, min.Value.Value);
        var range = RocBankValue.RoundUnits(ExactVarianceWindow.Units(highest) - ExactVarianceWindow.Units(lowest), BigInteger.One);
        var result = 0d;
        if (range.Sign > 0)
        {
            var ratio = ExactMeanAccumulator.UnitRatio(sum << 1074, range);
            // Keep the ordinary binary64 ratio; use logarithms of integers only when that ratio overflows.
            var logarithm = double.IsInfinity(ratio) ? (BigInteger.Log(sum) - BigInteger.Log(range)) / Math.Log(10) : Math.Log10(ratio);
            result = 100 * logarithm / Math.Log10(_length);
        }
        if (commit)
        {
            if (_ranges.Count == _length) _ranges.Dequeue(); _ranges.Enqueue(tr); _sum = sum;
            Commit(_high, high, true); Commit(_low, low, false); _previous = close; _count++;
        }
        return result;
    }
    internal void Reset() { _ranges.Clear(); _high.Clear(); _low.Clear(); _sum = default; _count = 0; _previous = 0; }
}
