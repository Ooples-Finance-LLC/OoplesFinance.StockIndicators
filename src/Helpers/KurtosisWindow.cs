namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KurtosisWindow : IDisposable
{
    private readonly int _first, _second;
    private readonly Queue<double> _prices = new();
    private readonly Queue<RocBankValue> _differences = new();
    private readonly RocBankAverage _slow, _fast;
    private long _count;
    internal KurtosisWindow(int first = 3, int second = 1, int fast = 3, int slow = 65, int capacity = int.MaxValue)
    { _first = Math.Max(1, first); _second = Math.Max(1, second); _slow = new(MovingAvgType.ExponentialMovingAverage, Math.Max(1, slow), capacity); _fast = new(MovingAvgType.WeightedMovingAverage, Math.Max(1, fast), capacity); }
    internal RocBankValue Difference(double price, bool commit)
    {
        var change = new ExactMeanAccumulator();
        if (_count >= _first) { change.Add(price); change.Add(_prices.Peek(), -1); }
        var difference = RocBankValue.Round(change); var second = new ExactMeanAccumulator();
        if (_count >= _second) { difference.AddTo(ref second); _differences.Peek().AddTo(ref second, -1); }
        var value = RocBankValue.Round(second);
        if (commit)
        {
            if (_prices.Count == _first) _prices.Dequeue(); _prices.Enqueue(price);
            if (_differences.Count == _second) _differences.Dequeue(); _differences.Enqueue(difference); _count++;
        }
        return value;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var value = Difference(price, commit); var line = _slow.Next(value, commit); var signal = _fast.Next(line, commit); return (line.Publish(), signal.Publish()); }
    internal void Reset() { _prices.Clear(); _differences.Clear(); _count = 0; _slow.Reset(); _fast.Reset(); }
    public void Dispose() { _slow.Dispose(); _fast.Dispose(); }
}
