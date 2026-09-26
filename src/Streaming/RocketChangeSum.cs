namespace OoplesFinance.StockIndicators.Streaming;

// Rocket RSI divides two small sums after its filter settles. Prefix differences and subtractive
// rolling totals retain error from expired impulses. This tree sums only the active window.
internal sealed class RocketChangeSum
{
    private readonly double[] _tree;
    private readonly int _length;
    private int _next;
    internal RocketChangeSum(int length)
    { _length = Math.Max(1, length); _tree = new double[checked(2 * _length)]; }
    internal double Next(double value, bool commit)
    {
        var node = _length + _next;
        var sum = value;
        if (commit) _tree[node] = sum;
        while (node > 1)
        {
            sum += _tree[node ^ 1];
            node /= 2;
            if (commit) _tree[node] = sum;
        }
        if (commit) _next = (_next + 1) % _length;
        return sum;
    }
    internal void Reset() { Array.Clear(_tree, 0, _tree.Length); _next = 0; }
}
