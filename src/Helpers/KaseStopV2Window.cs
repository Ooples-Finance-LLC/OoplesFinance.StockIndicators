using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KaseStopV2Window : IDisposable
{
    private readonly int _length;
    private readonly double[] _multiples;
    private readonly Average? _fast, _slow, _mean;
    private readonly Queue<BigInteger> _ranges = new();
    private BigInteger _sum, _squares;
    private double _previousHigh, _previousLow, _previousClose, _olderClose;
    private ExactMeanAccumulator _difference;
    internal KaseStopV2Window(MovingAvgType kind, int fast, int slow, int length, double first, double second, double third, double fourth, bool external = false)
    {
        _multiples = new[] { first, second, third, fourth }; if (_multiples.Any(v => double.IsNaN(v) || double.IsInfinity(v))) throw new ArgumentOutOfRangeException(nameof(first));
        _length = Math.Max(1, length); if (!external) { _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow)); _mean = new(kind, _length); }
    }
    internal static RocBankValue Range(double high, double low, double previousHigh, double previousLow, double olderClose)
    {
        var top = Math.Max(Math.Max(high, previousHigh), olderClose); var bottom = Math.Min(Math.Min(low, previousLow), olderClose);
        var sum = new ExactMeanAccumulator(); sum.Add(top); sum.Add(bottom, -1); return RocBankValue.Round(sum);
    }
    internal static double[] PublishedRanges(IReadOnlyList<double> high, IReadOnlyList<double> low, IReadOnlyList<double> close)
    {
        var result = new double[close.Count]; for (var i = 0; i < result.Length; i++) result[i] = Range(high[i], low[i], i == 0 ? 0 : high[i - 1], i == 0 ? 0 : low[i - 1], i < 2 ? 0 : close[i - 2]).Publish(); return result;
    }
    private RocBankValue Deviation(RocBankValue range, bool commit)
    {
        var units = ExactVarianceWindow.Units(range.Mantissa) << range.UpperShift; var full = _ranges.Count == _length; var expired = full ? _ranges.Peek() : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length); var deviation = default(RocBankValue);
        if (_ranges.Count + 1L >= _length) for (var shift = 0; ; shift += 32) { var value = ExactPopulationDeviation.RootRatio(n * squares - sum * sum, (n * n) << (2 * shift)); if (!double.IsInfinity(value)) { deviation = new(value, shift); break; } }
        if (commit) { _sum = sum; _squares = squares; if (full) _ranges.Dequeue(); _ranges.Enqueue(units); } return deviation;
    }
    internal (double Dev1, double Dev2, double Dev3, double Dev4, Signal Signal) Next(double high, double low, double close, bool commit, double? externalFast = null, double? externalSlow = null, double? externalMean = null)
    {
        var fast = externalFast.HasValue ? new RocBankValue(externalFast.Value) : _fast!.Next(new(close), commit); var slow = externalSlow.HasValue ? new RocBankValue(externalSlow.Value) : _slow!.Next(new(close), commit);
        var spread = new ExactMeanAccumulator(); fast.AddTo(ref spread); slow.AddTo(ref spread, -1); var up = spread.Sign > 0; var price = up ? high : low;
        var range = Range(high, low, _previousHigh, _previousLow, _olderClose); var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(range, commit); var deviation = Deviation(range, commit);
        RocBankValue Stop(int slot) { var sum = new ExactMeanAccumulator(); sum.AddProduct(deviation.Mantissa, _multiples[slot], up ? -1 : 1); sum.ScaleByPowerOfTwo(deviation.UpperShift); mean.AddTo(ref sum, up ? -1 : 1); sum.Add(price); return RocBankValue.Round(sum); }
        var first = Stop(0); var second = Stop(1); var third = Stop(2); var fourth = Stop(3);
        var difference = new ExactMeanAccumulator(); difference.Add(price); fourth.AddTo(ref difference, -1); var change = difference; change.Subtract(_difference);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _previousHigh = high; _previousLow = low; _olderClose = _previousClose; _previousClose = close; _difference = difference; }
        return (first.Publish(), second.Publish(), third.Publish(), fourth.Publish(), signal);
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _mean?.Reset(); _ranges.Clear(); _sum = _squares = default; _previousHigh = _previousLow = _previousClose = _olderClose = 0; _difference = default; }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _mean?.Dispose(); }
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
