namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>A bounded variable-window mean without subtraction of historical prefix totals.</summary>
internal sealed class AdaptiveWindowMean
{
    private readonly int _capacity;
    private readonly int _leaves;
    private readonly double[] _tree;
    private int _next;
    private int _count;

    internal AdaptiveWindowMean(int capacity)
    {
        _capacity = Math.Max(1, capacity);
        _leaves = 1;
        while (_leaves < _capacity) _leaves *= 2;
        _tree = new double[2 * _leaves];
    }

    internal double Next(double value, int window, bool isFinal)
    {
        var count = Math.Min(Math.Min(Math.Max(1, window), _capacity), _count + 1);
        var previous = count - 1;
        var start = (_next - previous + _capacity) % _capacity;
        var sum = start <= _next ? Sum(start, _next) : Sum(start, _capacity) + Sum(0, _next);
        var mean = (sum + value) / count;
        if (isFinal)
        {
            var node = _leaves + _next;
            _tree[node] = value;
            while ((node /= 2) > 0) _tree[node] = _tree[2 * node] + _tree[2 * node + 1];
            _next = (_next + 1) % _capacity;
            _count = Math.Min(_capacity, _count + 1);
        }
        return mean;
    }

    private double Sum(int start, int end)
    {
        double sum = 0;
        for (int left = start + _leaves, right = end + _leaves; left < right; left /= 2, right /= 2)
        {
            if ((left & 1) != 0) sum += _tree[left++];
            if ((right & 1) != 0) sum += _tree[--right];
        }
        return sum;
    }

    internal void Reset()
    {
        Array.Clear(_tree, 0, _tree.Length);
        _next = _count = 0;
    }
}
