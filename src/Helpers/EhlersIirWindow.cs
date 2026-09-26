namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EhlersIirWindow : IDisposable
{
    private readonly double _alpha;
    private readonly PooledRingBuffer<double> _prices;
    private RocBankValue _previous;
    internal EhlersIirWindow(int length)
    {
        _alpha = 2d / (Math.Max(1, length) + 1d);
        var lag = Math.Min(530, Math.Max(2, (int)Math.Ceiling(1 / _alpha - 1)));
        _prices = new(lag);
    }
    internal double Next(double price, bool commit)
    {
        var change = new ExactMeanAccumulator();
        if (_prices.Count == _prices.Capacity) { change.Add(price); change.Add(_prices[0], -1); }
        var momentum = RocBankValue.Round(change);
        var lead = new ExactMeanAccumulator(); lead.Add(price); momentum.AddTo(ref lead);
        var adjusted = RocBankValue.Round(lead);
        var current = adjusted.Multiply(_alpha); var retained = _previous.Multiply(1 - _alpha);
        var total = new ExactMeanAccumulator(); current.AddTo(ref total); retained.AddTo(ref total);
        var value = RocBankValue.Round(total);
        if (commit) { _previous = value; _prices.TryAdd(price, out _); }
        return value.Publish();
    }
    internal void Reset() { _prices.Clear(); _previous = default; }
    public void Dispose() => _prices.Dispose();
}
