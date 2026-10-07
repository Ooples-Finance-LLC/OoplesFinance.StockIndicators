using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
// Normalize the extended lag derivative by its fixed-denominator RMS before clipping.
internal sealed class EhlersClipWindow : IDisposable
{
    private readonly int _lag, _length;
    private readonly Queue<double> _prices = new();
    private readonly Queue<BigInteger> _derivatives = new();
    private readonly Average _signal;
    private BigInteger _squares;
    private double _last, _older, _oldest;
    internal EhlersClipWindow(MovingAvgType kind, int lag, int length, int signal)
    { _lag = Math.Max(1, lag); _length = Math.Max(1, length); _signal = new(kind, Math.Max(1, signal)); }
    internal double Line(double price, bool commit)
    {
        var change = new ExactMeanAccumulator(); if (_prices.Count == _lag) { change.Add(price); change.Add(_prices.Peek(), -1); }
        var difference = RocBankValue.Round(change); var units = ExactVarianceWindow.Units(difference.Mantissa) << difference.UpperShift;
        var expired = _derivatives.Count == _length ? _derivatives.Peek() : BigInteger.Zero;
        var squares = _squares + units * units - expired * expired;
        // Both the numerator and RMS may exceed binary64 or be subnormal; the bounded ratio does not.
        var numerator = 4 * units * units * _length;
        var clip = squares.IsZero ? 0 : numerator >= squares ? units.Sign : units.Sign * ExactPopulationDeviation.RootRatio(numerator << 2148, squares);
        var line = ((clip + _last) + _older) + _oldest;
        if (commit)
        {
            if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price);
            if (_derivatives.Count == _length) _derivatives.Dequeue(); _derivatives.Enqueue(units); _squares = squares;
            _oldest = _older; _older = _last; _last = clip;
        }
        return line;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var line = Line(price, commit); return (line, _signal.Next(new RocBankValue(line), commit).Publish()); }
    internal void Reset() { _prices.Clear(); _derivatives.Clear(); _squares = default; _last = _older = _oldest = 0; _signal.Reset(); }
    public void Dispose() => _signal.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
