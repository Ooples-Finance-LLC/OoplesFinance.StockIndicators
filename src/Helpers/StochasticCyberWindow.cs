namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class StochasticCyberWindow
{
    private readonly CyberCycleWindow _cycle;
    private readonly int _length;
    private readonly List<RocBankValue> _history = new();
    private double _raw1, _raw2, _raw3, _previous;
    internal StochasticCyberWindow(int length, double alpha) { _length = Math.Max(2, length); _cycle = new(alpha); }
    private static int Compare(RocBankValue left, RocBankValue right)
    { var difference = new ExactMeanAccumulator(); left.AddTo(ref difference); right.AddTo(ref difference, -1); return Math.Sign(difference.Mean(1)); }
    internal (double Line, double Signal) Next(double value, bool commit)
    {
        var cycle = _cycle.NextExtended(value, commit); var low = cycle; var high = cycle;
        var start = _history.Count == _length ? 1 : 0;
        for (var i = start; i < _history.Count; i++)
        { var v = _history[i]; if (Compare(v, low) < 0) low = v; if (Compare(v, high) > 0) high = v; }
        var range = new ExactMeanAccumulator(); high.AddTo(ref range); low.AddTo(ref range, -1);
        var numerator = new ExactMeanAccumulator(); cycle.AddTo(ref numerator); low.AddTo(ref numerator, -1);
        var raw = range.IsExactlyZero ? 0 : Math.Max(0, Math.Min(1, numerator.Ratio(range)));
        var line = Math.Max(-1, Math.Min(1, 2 * ((4 * raw + 3 * _raw1 + 2 * _raw2 + _raw3) / 10 - .5)));
        var signal = Math.Max(-1, Math.Min(1, .96 * (_previous + .02)));
        if (commit) { if (_history.Count == _length) _history.RemoveAt(0); _history.Add(cycle); _raw3 = _raw2; _raw2 = _raw1; _raw1 = raw; _previous = line; }
        return (line, signal);
    }
    internal void Reset() { _cycle.Reset(); _history.Clear(); _raw1 = _raw2 = _raw3 = _previous = 0; }
}
