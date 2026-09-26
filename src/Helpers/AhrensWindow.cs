namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AhrensWindow : IDisposable
{
    private readonly PooledRingBuffer<RocBankValue> _history;
    private RocBankValue _previous;
    internal AhrensWindow(int length) => _history = new(Math.Max(1, length));
    internal double Next(double price, bool commit)
    {
        var prior = _history.Count == _history.Capacity ? _history[0] : new RocBankValue(price);
        var sum = new ExactMeanAccumulator(); _previous.AddTo(ref sum); prior.AddTo(ref sum);
        var roundedSum = RocBankValue.Round(sum);
        var midpoint = new ExactMeanAccumulator(); roundedSum.AddTo(ref midpoint);
        var center = RocBankValue.Round(midpoint, count: 2);
        var gap = new ExactMeanAccumulator(); gap.Add(price); center.AddTo(ref gap, -1);
        var roundedGap = RocBankValue.Round(gap);
        var step = new ExactMeanAccumulator(); roundedGap.AddTo(ref step);
        var increment = RocBankValue.Round(step, count: _history.Capacity);
        var result = new ExactMeanAccumulator(); _previous.AddTo(ref result); increment.AddTo(ref result);
        var value = RocBankValue.Round(result);
        if (commit) { _previous = value; _history.TryAdd(value, out _); }
        return value.Publish();
    }
    internal void Reset() { _history.Clear(); _previous = default; }
    public void Dispose() => _history.Dispose();
}
