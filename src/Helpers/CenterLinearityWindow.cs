namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CenterLinearityWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly Queue<RocBankValue> _terms = new();
    private ExactMeanAccumulator _total;
    private double _previous;
    private long _index;
    internal CenterLinearityWindow(int length) => _length = Math.Max(1, length);
    internal double Next(double price, bool commit)
    {
        var prior = _prices.Count == _length ? _prices.Peek() : 0;
        var difference = new ExactMeanAccumulator(); difference.Add(prior); difference.Add(_previous, -1);
        var roundedDifference = RocBankValue.Round(difference);
        var product = new ExactMeanAccumulator(); roundedDifference.AddTo(ref product, _index + 1);
        var term = RocBankValue.Round(product);
        var total = _total;
        if (_terms.Count == _length) _terms.Peek().AddTo(ref total, -1);
        term.AddTo(ref total);
        if (commit)
        {
            _total = total; _previous = price; _index++;
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_terms.Count == _length) _terms.Dequeue(); _terms.Enqueue(term);
        }
        return total.Mean(1);
    }
    internal void Reset() { _prices.Clear(); _terms.Clear(); _total = default; _previous = 0; _index = 0; }
    public void Dispose() { _prices.Clear(); _terms.Clear(); }
}
