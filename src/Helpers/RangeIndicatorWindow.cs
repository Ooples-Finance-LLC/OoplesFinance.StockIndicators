using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RangeIndicatorWindow
{
    private readonly int _length;
    private readonly LinkedList<(long Index, BigInteger Value)> _maximum = new(), _minimum = new();
    private long _count;
    private double _previous;
    internal RangeIndicatorWindow(int length) => _length = Math.Max(1, length);
    private void Commit(LinkedList<(long Index, BigInteger Value)> values, BigInteger value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    internal double Next(double high, double low, double close, bool commit)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var c = ExactVarianceWindow.Units(close); var p = ExactVarianceWindow.Units(_count == 0 ? close : _previous);
        var tr = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p)));
        var value = RocBankValue.RoundUnits(tr, BigInteger.One);
        if (_count > 0 && close > _previous)
        {
            var gain = RocBankValue.RoundUnits(c - p, BigInteger.One);
            value = RocBankValue.RoundUnits(value << 1074, gain);
        }
        var max = _maximum.First; while (max is not null && max.Value.Index <= _count - _length) max = max.Next;
        var min = _minimum.First; while (min is not null && min.Value.Index <= _count - _length) min = min.Next;
        var upper = max is null ? value : BigInteger.Max(value, max.Value.Value);
        var lower = min is null ? value : BigInteger.Min(value, min.Value.Value);
        var result = upper == lower ? 0 : ExactMeanAccumulator.UnitRatio((100 * (value - lower)) << 1074, upper - lower);
        if (commit) { Commit(_maximum, value, true); Commit(_minimum, value, false); _previous = close; _count++; }
        return result;
    }
    internal void Reset() { _maximum.Clear(); _minimum.Clear(); _count = 0; _previous = 0; }
}
