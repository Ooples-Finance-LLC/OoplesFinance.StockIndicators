namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VolatilityRatioWindow
{
    private readonly int _rangeLength; private readonly long _lag;
    private readonly Queue<double> _prices = new(); private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private long _count; private double _previous;
    internal VolatilityRatioWindow(int length) { length = Math.Max(1, length); _rangeLength = Math.Max(1, length - 1); _lag = length + 1L; }
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _rangeLength) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    private static RocBankValue Difference(double a, double b)
    { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b, -1); return RocBankValue.Round(sum); }
    private static RocBankValue Abs(RocBankValue value) => new(Math.Abs(value.Mantissa), value.UpperShift);
    private static RocBankValue Max(RocBankValue a, RocBankValue b)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return sum.Sign >= 0 ? a : b; }
    internal double Next(double high, double low, double price, bool commit)
    {
        var previous = _count == 0 ? price : _previous; var prior = _prices.Count == _lag ? _prices.Peek() : 0;
        var maximum = _high.First?.Value.Value ?? 0; var minimum = _low.First?.Value.Value ?? 0;
        if (prior != 0) { maximum = Math.Max(maximum, prior); minimum = Math.Min(minimum, prior); }
        var range = Max(Difference(high, low), Max(Abs(Difference(high, previous)), Abs(Difference(low, previous))));
        var numerator = new ExactMeanAccumulator(); range.AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); Difference(maximum, minimum).AddTo(ref denominator); var result = numerator.Ratio(denominator);
        if (commit) { Commit(_high, high, true); Commit(_low, low, false); if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); _previous = price; _count++; }
        return result;
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _prices.Clear(); _count = 0; _previous = 0; }
}
