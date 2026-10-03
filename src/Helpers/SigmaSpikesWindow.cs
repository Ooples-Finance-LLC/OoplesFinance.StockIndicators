using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SigmaSpikesWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<BigInteger> _returns = new();
    private readonly Average _signal;
    private BigInteger _sum, _squares;
    private double _previousPrice;
    private RocBankValue _previousDeviation;
    internal SigmaSpikesWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _signal = new(kind, _length); }
    private static RocBankValue Root(BigInteger numerator, BigInteger denominator)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift)); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private static RocBankValue Ratio(RocBankValue value, RocBankValue divisor)
    {
        var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); divisor.AddTo(ref denominator);
        for (var shift = 0; ; shift += 32)
        { var scaled = denominator; scaled.ScaleByPowerOfTwo(shift); var result = numerator.Ratio(scaled); if (!double.IsInfinity(result)) return new(result, shift); }
    }
    internal RocBankValue Line(double price, bool commit)
    {
        var ret = default(RocBankValue);
        if (_previousPrice != 0)
        {
            var numerator = new ExactMeanAccumulator(); numerator.Add(price); var ratio = RocBankValue.Round(numerator, _previousPrice);
            var difference = new ExactMeanAccumulator(); ratio.AddTo(ref difference); difference.Add(-1); ret = RocBankValue.Round(difference);
        }
        var units = ExactVarianceWindow.Units(ret.Mantissa) << ret.UpperShift; var full = _returns.Count == _length; var expired = full ? _returns.Peek() : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length);
        var deviation = _returns.Count + 1L < _length ? default : Root(n * squares - sum * sum, n * n);
        var line = Ratio(ret, _previousDeviation);
        if (commit) { _previousPrice = price; _previousDeviation = deviation; _sum = sum; _squares = squares; if (full) _returns.Dequeue(); _returns.Enqueue(units); }
        return line;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var line = Line(price, commit); return (line.Publish(), _signal.Next(line, commit).Publish()); }
    internal void Reset() { _returns.Clear(); _sum = _squares = default; _previousPrice = 0; _previousDeviation = default; _signal.Reset(); }
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
