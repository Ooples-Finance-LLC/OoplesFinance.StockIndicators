namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RecursiveMedianWindow
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly Queue<(double Value, long Index)> _history = new();
    private readonly SortedSet<(double Value, long Index)> _lower = new(), _upper = new();
    private long _index;
    private double _previous;
    internal RecursiveMedianWindow(int length, int smoothing)
    {
        _length = Math.Max(1, length);
        var angle = MathHelper.MinOrMax(2 * Math.PI / Math.Max(1, smoothing), .99, .01);
        _alpha = (Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle);
    }
    private void Balance()
    {
        while (_lower.Count > _upper.Count + 1) { var item = _lower.Max; _lower.Remove(item); _upper.Add(item); }
        while (_upper.Count > _lower.Count) { var item = _upper.Min; _upper.Remove(item); _lower.Add(item); }
    }
    private void Insert((double Value, long Index) item)
    {
        if (_lower.Count == 0 || item.CompareTo(_lower.Max) <= 0) _lower.Add(item); else _upper.Add(item);
        Balance();
    }
    private void Remove((double Value, long Index) item)
    {
        if (!_lower.Remove(item)) _upper.Remove(item);
        Balance();
    }
    internal double Next(double price, bool commit)
    {
        var full = _history.Count == _length;
        var expired = full ? _history.Peek() : default;
        if (full) Remove(expired);
        var current = (price, _index);
        Insert(current);
        var middle = new ExactMeanAccumulator(); middle.Add(_lower.Max.Value);
        var even = _lower.Count == _upper.Count;
        if (even) middle.Add(_upper.Min.Value);
        var median = middle.Mean(even ? 2 : 1);
        // Exact interpolation avoids overflow in both the midpoint and feedback difference.
        var next = new ExactMeanAccumulator(); next.Add(_previous);
        next.AddProduct(_alpha, median); next.AddProduct(_alpha, _previous, -1);
        var result = next.Mean(1);
        if (commit)
        {
            if (full) _history.Dequeue();
            _history.Enqueue(current); _index++; _previous = result;
        }
        else
        {
            Remove(current); if (full) Insert(expired);
        }
        return result;
    }
    internal void Reset() { _history.Clear(); _lower.Clear(); _upper.Clear(); _index = 0; _previous = 0; }
}
