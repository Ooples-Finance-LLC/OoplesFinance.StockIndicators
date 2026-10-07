namespace OoplesFinance.StockIndicators.Helpers;

// Retain only observed history: even a maximum period can accept a short source.
internal sealed class SelfWeightedWindow : IDisposable
{
    private readonly int _length;
    private readonly long _capacity;
    private readonly List<double> _history = new();
    private int _start;
    internal SelfWeightedWindow(int length) { _length = Math.Max(1, length); _capacity = 2L * _length; }
    private double At(double current, int offset) => offset == 0 ? current :
        _history[(int)((_start + (long)_history.Count - offset) % _history.Count)];
    internal double Next(double current, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator();
        var terms = Math.Min(_length, Math.Max(0, _history.Count - _length + 1));
        for (var j = 0; j < terms; j++)
        {
            var weight = At(current, _length + j);
            numerator.AddProduct(At(current, j), weight); denominator.Add(weight);
        }
        var result = numerator.Ratio(denominator);
        if (commit)
        {
            if (_history.Count < _capacity) _history.Add(current);
            else { _history[_start] = current; _start = (_start + 1) % _history.Count; }
        }
        return result;
    }
    internal void Reset() { _history.Clear(); _start = 0; }
    public void Dispose() => _history.Clear();
}
