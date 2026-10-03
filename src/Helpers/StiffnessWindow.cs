using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class StiffnessWindow : IDisposable
{
    private readonly int _length, _lookback;
    private readonly Average _average;
    private readonly RocBankAverage _signal;
    private readonly Queue<double> _prices = new();
    private readonly Queue<bool> _flags = new();
    private BigInteger _sum, _squares;
    private int _votes;
    internal StiffnessWindow(MovingAvgType kind, int length, int lookback, int smoothing)
    { _length = Math.Max(1, length); _lookback = Math.Max(1, lookback); _average = new(kind, _length); _signal = new(MovingAvgType.ExponentialMovingAverage, Math.Max(1, smoothing), 1); }
    internal double Vote(double price, bool commit, double? externalAverage = null)
    {
        var average = externalAverage ?? _average.Next(new RocBankValue(price), commit).Publish();
        var units = ExactVarianceWindow.Units(price); var expired = _prices.Count == _length ? ExactVarianceWindow.Units(_prices.Peek()) : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length);
        var deviation = _prices.Count + 1L < _length ? 0 : ExactPopulationDeviation.RootRatio(n * squares - sum * sum, n * n);
        var bound = average - .2 * deviation; var above = price > bound;
        var votes = _votes - (_flags.Count == _lookback && _flags.Peek() ? 1 : 0) + (above ? 1 : 0);
        if (commit)
        {
            _sum = sum; _squares = squares; if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            _votes = votes; if (_flags.Count == _lookback) _flags.Dequeue(); _flags.Enqueue(above);
        }
        return votes * 100d / _lookback;
    }
    internal double Next(double price, bool commit) => _signal.Next(new RocBankValue(Vote(price, commit)), commit).Publish();
    internal void Reset() { _prices.Clear(); _flags.Clear(); _sum = _squares = default; _votes = 0; _average.Reset(); _signal.Reset(); }
    public void Dispose() { _average.Dispose(); _signal.Dispose(); }
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
