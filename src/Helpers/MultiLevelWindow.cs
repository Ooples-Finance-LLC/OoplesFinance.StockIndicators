namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MultiLevelWindow
{
    private readonly int _length; private readonly double _factor; private readonly Queue<double> _history = new();
    internal MultiLevelWindow(int length, double factor)
    { if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor)); _length = Math.Max(1, length); _factor = factor; }
    internal static double Difference(double previous, double current, double factor)
    { var sum = new ExactMeanAccumulator(); sum.Add(previous); sum.Add(current, -1); return RocBankValue.Round(sum).Multiply(factor).Publish(); }
    internal double Next(double open, bool commit)
    {
        var full = _history.Count == _length; var previous = full ? _history.Peek() : 0; var result = Difference(previous, open, _factor);
        if (commit) { if (full) _history.Dequeue(); _history.Enqueue(open); }
        return result;
    }
    internal void Reset() => _history.Clear();
}
