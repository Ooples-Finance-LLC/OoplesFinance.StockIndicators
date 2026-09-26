namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class LaggedBalanceWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<(double From, double To)> _changes;
    private ExactMeanAccumulator _net, _absolute;
    internal LaggedBalanceWindow(int length)
    { length = Math.Max(1, length); _prices = new(length); _changes = new(length); }
    private static void Add(ref ExactMeanAccumulator net, ref ExactMeanAccumulator absolute, double from, double to, int weight)
    {
        net.Add(to, weight); net.Add(from, -weight);
        var direction = to >= from ? weight : -weight;
        absolute.Add(to, direction); absolute.Add(from, -direction);
    }
    internal double Next(double value, bool commit)
    {
        var from = _prices.Count == _prices.Capacity ? _prices[0] : value;
        var net = _net; var absolute = _absolute;
        if (_changes.Count == _changes.Capacity)
        { var expired = _changes[0]; Add(ref net, ref absolute, expired.From, expired.To, -1); }
        Add(ref net, ref absolute, from, value, 1);
        var result = net.Ratio(absolute);
        if (commit)
        { _prices.TryAdd(value, out _); _changes.TryAdd((from, value), out _); _net = net; _absolute = absolute; }
        return result;
    }
    internal void Reset() { _prices.Clear(); _changes.Clear(); _net = _absolute = default; }
    public void Dispose() { _prices.Dispose(); _changes.Dispose(); }
}
