namespace OoplesFinance.StockIndicators.Helpers;

// Buff uses the observed partial window, unlike the full-window VWMA startup.
internal sealed class BuffWindow
{
    private readonly int _length;
    private readonly Queue<(double Price, double Volume)> _terms = new();
    private ExactMeanAccumulator _weighted, _volume;
    internal BuffWindow(int length) => _length = Math.Max(1, length);
    internal double Next(double price, double volume, bool commit)
    {
        var weighted = _weighted; var mass = _volume;
        if (_terms.Count == _length)
        {
            var old = _terms.Peek(); weighted.AddProduct(old.Price, old.Volume, -1); mass.Add(old.Volume, -1);
        }
        weighted.AddProduct(price, volume); mass.Add(volume);
        var result = weighted.Ratio(mass);
        if (commit)
        {
            if (_terms.Count == _length) _terms.Dequeue();
            _terms.Enqueue((price, volume)); _weighted = weighted; _volume = mass;
        }
        return result;
    }
    internal void Reset() { _terms.Clear(); _weighted = _volume = default; }
}
