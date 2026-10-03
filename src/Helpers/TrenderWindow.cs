using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrenderWindow : IDisposable
{
    private readonly int _length; private readonly double _factor;
    private readonly Average? _priceMean, _rangeMean, _adaptiveMean;
    private readonly Queue<BigInteger> _ranges = new(); private BigInteger _sum, _squares;
    private bool _seeded; private int _side; private double _previous, _high1, _high2, _low1, _low2;
    private RocBankValue _up, _down, _line; private ExactMeanAccumulator _difference;
    internal TrenderWindow(MovingAvgType kind, int length, double factor, bool external = false)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _length = Math.Max(1, length); _factor = factor; if (!external) { _priceMean = new(kind, _length); _rangeMean = new(kind, _length); _adaptiveMean = new(kind, _length); }
    }
    private static RocBankValue Range(double high, double low, double previous)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous);
        var units = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p))); var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum);
    }
    private static RocBankValue Adaptive(RocBankValue mean, RocBankValue atr, int direction)
    { var half = atr.Multiply(.5); var sum = new ExactMeanAccumulator(); mean.AddTo(ref sum); half.AddTo(ref sum, direction); return RocBankValue.Round(sum); }
    internal static double[] PublishedAdaptive(IReadOnlyList<double> prices, IReadOnlyList<double> mean, IReadOnlyList<double> atr)
    { var result = new double[prices.Count]; for (var i = 0; i < result.Length; i++) result[i] = Adaptive(new(mean[i]), new(atr[i]), prices[i].CompareTo(i == 0 ? 0 : prices[i - 1])).Publish(); return result; }
    private RocBankValue Deviation(RocBankValue atr, bool commit)
    {
        var units = ExactVarianceWindow.Units(atr.Mantissa) << atr.UpperShift; var full = _ranges.Count == _length; var expired = full ? _ranges.Peek() : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length); var deviation = default(RocBankValue);
        if (_ranges.Count + 1L >= _length) for (var shift = 0; ; shift += 32) { var value = ExactPopulationDeviation.RootRatio(n * squares - sum * sum, (n * n) << (2 * shift)); if (!double.IsInfinity(value)) { deviation = new(value, shift); break; } }
        if (commit) { _sum = sum; _squares = squares; if (full) _ranges.Dequeue(); _ranges.Enqueue(units); } return deviation;
    }
    internal (double TrendUp, double TrendDn, double Trender, Signal Signal) Next(double high, double low, double close, bool commit, double? externalPrice = null, double? externalAtr = null, double? externalAdaptive = null)
    {
        var mean = externalPrice.HasValue ? new RocBankValue(externalPrice.Value) : _priceMean!.Next(new(close), commit);
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _rangeMean!.Next(Range(high, low, _seeded ? _previous : close), commit);
        var direction = close.CompareTo(_previous); var adaptive = externalAdaptive.HasValue ? new RocBankValue(externalAdaptive.Value) : _adaptiveMean!.Next(Adaptive(mean, atr, direction), commit);
        var comparison = new ExactMeanAccumulator(); adaptive.AddTo(ref comparison); mean.AddTo(ref comparison, -1); var side = comparison.Sign; var deviation = Deviation(atr, commit);
        RocBankValue Stop(int sign) { var sum = new ExactMeanAccumulator(); sum.AddProduct(deviation.Mantissa, _factor, sign); sum.ScaleByPowerOfTwo(deviation.UpperShift); sum.Add(close); return RocBankValue.Round(sum); }
        var down = side < 0 && _side > 0 ? new RocBankValue(_high2) : direction < 0 ? Stop(1) : _down;
        var up = side > 0 && _side < 0 ? new RocBankValue(_low2) : direction > 0 ? Stop(-1) : _up;
        var line = side < 0 ? down : side > 0 ? up : _line;
        var difference = new ExactMeanAccumulator(); difference.Add(close); line.AddTo(ref difference, -1); var change = difference; change.Subtract(_difference);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _seeded = true; _previous = close; _side = side; _high2 = _high1; _high1 = high; _low2 = _low1; _low1 = low; _up = up; _down = down; _line = line; _difference = difference; }
        return (up.Publish(), down.Publish(), line.Publish(), signal);
    }
    internal void Reset() { _priceMean?.Reset(); _rangeMean?.Reset(); _adaptiveMean?.Reset(); _ranges.Clear(); _sum = _squares = default; _seeded = false; _side = 0; _previous = _high1 = _high2 = _low1 = _low2 = 0; _up = _down = _line = default; _difference = default; }
    public void Dispose() { _priceMean?.Dispose(); _rangeMean?.Dispose(); _adaptiveMean?.Dispose(); }
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
