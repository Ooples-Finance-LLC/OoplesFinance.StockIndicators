namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AdaptiveRangeMeanWindow : IDisposable
{
    private readonly long _period;
    private readonly double _fastAlpha, _slowAlpha;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index;
    private double _previous;

    internal AdaptiveRangeMeanWindow(int fast, int slow, int length)
    {
        _period = Math.Max(1, length) + 1L;
        _fastAlpha = 2d / (Math.Max(1, fast) + 1L);
        _slowAlpha = 2d / (Math.Max(1, slow) + 1L);
    }

    private double Extreme(LinkedList<(long Index, double Value)> queue, double current, bool maximum)
    {
        var node = queue.First;
        while (node is not null && node.Value.Index <= _index - _period) node = node.Next;
        return node is null ? current : maximum ? Math.Max(current, node.Value.Value) : Math.Min(current, node.Value.Value);
    }

    private void Commit(LinkedList<(long Index, double Value)> queue, double current, bool maximum)
    {
        while (queue.First is not null && queue.First.Value.Index <= _index - _period) queue.RemoveFirst();
        while (queue.Last is not null && (maximum ? queue.Last.Value.Value <= current : queue.Last.Value.Value >= current)) queue.RemoveLast();
        queue.AddLast((_index, current));
    }

    internal double Next(double price, double high, double low, bool commit)
    {
        var highest = Extreme(_highs, high, true);
        var lowest = Extreme(_lows, low, false);
        var multiplier = 0d;
        if (highest != lowest)
        {
            var numerator = new ExactMeanAccumulator();
            numerator.Add(price, 2); numerator.Add(lowest, -1); numerator.Add(highest, -1);
            if (numerator.Sign < 0) { var negative = numerator; numerator = default; numerator.Subtract(negative); }
            var denominator = new ExactMeanAccumulator(); denominator.Add(highest); denominator.Add(lowest, -1);
            multiplier = Math.Max(0, Math.Min(1, numerator.Ratio(denominator)));
        }
        var coefficient = multiplier * (_fastAlpha - _slowAlpha) + _slowAlpha;
        var gain = coefficient * coefficient;
        var value = VidyaBlend.Compute(_previous, price, gain);
        if (commit)
        {
            Commit(_highs, high, true); Commit(_lows, low, false);
            _previous = value; _index++;
        }
        return value;
    }

    internal void Reset() { _highs.Clear(); _lows.Clear(); _previous = 0; _index = 0; }
    public void Dispose() { _highs.Clear(); _lows.Clear(); }
}
