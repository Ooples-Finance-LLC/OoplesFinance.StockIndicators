using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FastSlowKurtosisWindow : IDisposable
{
    private readonly KurtosisWindow _difference;
    private readonly double _ratio;
    private readonly int _length;
    private readonly MovingAvgType _kind;
    private readonly Queue<RocBankValue> _history = new();
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private ExactMeanAccumulator _sum, _weighted;
    private RocBankValue _previous;
    internal FastSlowKurtosisWindow(int length = 3, double ratio = .03, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio)) throw new ArgumentOutOfRangeException(nameof(ratio));
        _length = Math.Max(1, length); _ratio = ratio; _kind = kind;
        _difference = new KurtosisWindow(_length, 1, 1, 1);
        if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _average = new(kind, _length, 1);
        else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }
    internal RocBankValue Line(double price, bool commit)
    {
        var change = _difference.Difference(price, commit).Multiply(_ratio);
        var retained = _previous.Multiply(1 - _ratio); var sum = new ExactMeanAccumulator();
        change.AddTo(ref sum); retained.AddTo(ref sum); var line = RocBankValue.Round(sum);
        if (commit) _previous = line;
        return line;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    {
        var line = Line(price, commit); RocBankValue signal;
        if (_average is not null) signal = _average.Next(line, commit);
        else if (_fallback is not null) signal = new(_fallback.Next(line.Publish(), commit));
        else
        {
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); line.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); line.AddTo(ref sum);
            signal = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(line); }
        }
        return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _difference.Reset(); _previous = default; _sum = _weighted = default; _history.Clear(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _difference.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
