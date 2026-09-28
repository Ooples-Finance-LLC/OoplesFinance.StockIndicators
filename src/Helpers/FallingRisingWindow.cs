namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FallingRisingWindow
{
    private readonly int _length; private readonly double _alpha;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private long _count; private double _previous; private RocBankValue _line, _error;
    internal FallingRisingWindow(int length) { _length = Math.Max(2, length); _alpha = 2d / (_length + 1d); }
    private double Extreme(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        var node = values.First; while (node is not null && node.Value.Index <= _count - _length) node = node.Next;
        return node is null ? value : maximum ? Math.Max(value, node.Value.Value) : Math.Min(value, node.Value.Value);
    }
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal (double Line, double Error) Next(double price, bool commit)
    {
        var high = Extreme(_high, _previous, true); var low = Extreme(_low, _previous, false);
        var beta = price > high || price < low ? 1 : _alpha;
        var line = Add(Add(_line, _error.Multiply(_alpha)), _error.Multiply(beta)); var error = Add(new RocBankValue(price), line, -1);
        if (commit) { Commit(_high, _previous, true); Commit(_low, _previous, false); _previous = price; _line = line; _error = error; _count++; }
        return (line.Publish(), error.Publish());
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _count = 0; _previous = 0; _line = _error = default; }
}
