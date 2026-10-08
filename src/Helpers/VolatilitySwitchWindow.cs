using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VolatilitySwitchWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<BigInteger> _returns = new();
    private readonly Average _volatility, _price;
    private BigInteger _sum, _squares;
    private double _previous;
    internal VolatilitySwitchWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _volatility = new(kind, _length); _price = new(kind, _length); }
    private static RocBankValue Root(BigInteger numerator, BigInteger denominator)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift)); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    internal RocBankValue Deviation(double price, bool commit)
    {
        var midpoint = PriceMean.Of(price, _previous); var ret = default(RocBankValue);
        if (_returns.Count > 0 && midpoint != 0)
        {
            var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(_previous, -1); var delta = RocBankValue.Round(difference);
            var numerator = new ExactMeanAccumulator(); delta.AddTo(ref numerator); ret = RocBankValue.Round(numerator, midpoint);
        }
        var units = ExactVarianceWindow.Units(ret.Mantissa) << ret.UpperShift; var full = _returns.Count == _length; var expired = full ? _returns.Peek() : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length);
        var deviation = _returns.Count + 1L < _length ? default : Root(n * squares - sum * sum, n * n);
        if (commit) { _previous = price; _sum = sum; _squares = squares; if (full) _returns.Dequeue(); _returns.Enqueue(units); }
        return deviation;
    }
    internal double Value(double price, bool commit) => _volatility.Next(Deviation(price, commit), commit).Publish();
    internal double PriceAverage(double price, bool commit) => _price.Next(new RocBankValue(price), commit).Publish();
    internal void Reset() { _returns.Clear(); _sum = _squares = default; _previous = 0; _volatility.Reset(); _price.Reset(); }
    public void Dispose() { _volatility.Dispose(); _price.Dispose(); }
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
