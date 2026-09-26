namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolumeWeightedWindow : IDisposable
{
    private readonly int _length;
    private readonly VolumeAdjustedWindow? _average;
    private readonly Queue<(double Price, double Volume)> _terms = new();
    private ExactMeanAccumulator _sum, _mass;
    internal VolumeWeightedWindow(MovingAvgType kind, int length, bool initializeFallback = true)
    {
        _length = Math.Max(1, length);
        if (kind != MovingAvgType.SimpleMovingAverage) _average = new(kind, _length, initializeFallback: initializeFallback);
    }
    internal double Next(double price, double volume, bool commit) => Aggregate(price, volume, _average?.NextMean(volume, commit), commit);
    internal double NextWithAverage(double price, double volume, double average, bool commit) => Aggregate(price, volume, average, commit);
    private double Aggregate(double price, double volume, double? average, bool commit)
    {
        var sum = _sum; var mass = _mass;
        if (_terms.Count == _length) { var old = _terms.Peek(); sum.AddProduct(old.Price, old.Volume, -1); mass.Add(old.Volume, -1); }
        sum.AddProduct(price, volume); mass.Add(volume);
        var count = _terms.Count == _length ? _length : _terms.Count + 1;
        var denominator = mass;
        if (average is { } value) { denominator = new ExactMeanAccumulator(); denominator.Add(value, count); }
        var result = average is null && count < _length ? 0 : sum.Ratio(denominator);
        if (commit)
        {
            if (_terms.Count == _length) _terms.Dequeue(); _terms.Enqueue((price, volume)); _sum = sum; _mass = mass;
        }
        return result;
    }
    internal void Reset() { _average?.Reset(); _terms.Clear(); _sum = default; _mass = default; }
    public void Dispose() => _average?.Dispose();
}
