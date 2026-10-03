namespace OoplesFinance.StockIndicators.Helpers;

internal enum VolumeBalanceKind { UpsideDownside, Tfs, Accumulation }

// Price comparisons never subtract; weighted price/volume products and window
// totals remain exact until the final mean or ratio is published.
internal sealed class VolumeBalanceWindow : IDisposable
{
    private readonly record struct Contribution(double Price, double High, double Low, double Volume, int Direction);
    private readonly PooledRingBuffer<Contribution> _window;
    private readonly VolumeBalanceKind _kind;
    private ExactMeanAccumulator _up, _down;
    private double _previous;
    internal VolumeBalanceWindow(int length, VolumeBalanceKind kind)
    { _window = new(Math.Max(1, length)); _kind = kind; }
    private void Add(ref ExactMeanAccumulator up, ref ExactMeanAccumulator down, Contribution value, int weight)
    {
        if (_kind == VolumeBalanceKind.Accumulation)
        {
            up.AddProduct(value.Price, value.Volume, 2 * weight);
            up.AddProduct(value.High, value.Volume, -weight);
            up.AddProduct(value.Low, value.Volume, -weight);
        }
        else if (value.Direction > 0) up.Add(value.Volume, weight);
        else if (value.Direction < 0)
        {
            if (_kind == VolumeBalanceKind.UpsideDownside) down.Add(value.Volume, -weight);
            else up.Add(value.Volume, -weight);
        }
    }
    internal double Next(double open, double high, double low, double price, double volume, bool commit)
    {
        var baseline = _kind == VolumeBalanceKind.UpsideDownside ? _previous : open;
        var current = new Contribution(price, high, low, volume, price.CompareTo(baseline));
        var up = _up; var down = _down;
        if (_window.Count == _window.Capacity) Add(ref up, ref down, _window[0], -1);
        Add(ref up, ref down, current, 1);
        var count = Math.Min(_window.Count + 1, _window.Capacity);
        var value = _kind == VolumeBalanceKind.UpsideDownside ? up.Ratio(down)
            : up.Mean(_kind == VolumeBalanceKind.Tfs ? _window.Capacity : 2L * count);
        if (commit) { _window.TryAdd(current, out _); _up = up; _down = down; _previous = price; }
        return value;
    }
    internal void Reset() { _window.Clear(); _up = _down = default; _previous = 0; }
    public void Dispose() => _window.Dispose();
}
