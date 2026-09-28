namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RecursiveStochasticWindow
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new(), _blendHigh = new(), _blendLow = new();
    private long _count;
    private double _previous;
    internal RecursiveStochasticWindow(int length, double alpha)
    { if (double.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha)); _length = Math.Max(1, length); _alpha = Math.Max(0, Math.Min(1, alpha)); }
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
    private static double Percent(double value, double high, double low)
    {
        var distance = new ExactMeanAccumulator(); distance.Add(value); distance.Add(low, -1);
        var span = new ExactMeanAccumulator(); span.Add(high); span.Add(low, -1);
        var numerator = new ExactMeanAccumulator(); RocBankValue.Round(distance).AddTo(ref numerator);
        var denominator = new ExactMeanAccumulator(); RocBankValue.Round(span).AddTo(ref denominator);
        return numerator.Ratio(denominator) * 100;
    }
    internal double Next(double price, bool commit)
    {
        var stoch = Percent(price, Extreme(_high, price, true), Extreme(_low, price, false));
        var blend = _alpha * stoch + (1 - _alpha) * _previous;
        var result = Math.Max(0, Math.Min(100, Percent(blend, Extreme(_blendHigh, blend, true), Extreme(_blendLow, blend, false))));
        if (commit)
        { Commit(_high, price, true); Commit(_low, price, false); Commit(_blendHigh, blend, true); Commit(_blendLow, blend, false); _previous = result; _count++; }
        return result;
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _blendHigh.Clear(); _blendLow.Clear(); _previous = 0; _count = 0; }
}
