namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GopalakrishnanWindow
{
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private long _count;
    internal GopalakrishnanWindow(int length) => _length = Math.Max(2, length);
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    internal double Next(double high, double low, bool commit)
    {
        var max = _high.First; while (max is not null && max.Value.Index <= _count - _length) max = max.Next;
        var min = _low.First; while (min is not null && min.Value.Index <= _count - _length) min = min.Next;
        var highest = max is null ? high : Math.Max(high, max.Value.Value);
        var lowest = min is null ? low : Math.Min(low, min.Value.Value);
        var difference = new ExactMeanAccumulator(); difference.Add(highest); difference.Add(lowest, -1); var range = RocBankValue.Round(difference);
        var result = range.Mantissa > 0 ? (Math.Log(range.Mantissa) + range.UpperShift * Math.Log(2)) / Math.Log(_length) : 0;
        if (commit) { Commit(_high, high, true); Commit(_low, low, false); _count++; }
        return result;
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _count = 0; }
}
