using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KaseStopV1Window : IDisposable
{
    private readonly int _length;
    private readonly double[] _multiples;
    private readonly TrendAverage? _fast, _slow;
    private readonly Average? _mean;
    private readonly Queue<BigInteger> _ranges = new();
    private BigInteger _sum, _squares;
    private double _previousLow, _olderLow, _previousClose, _olderClose;
    private ExactMeanAccumulator _spread;
    internal KaseStopV1Window(MovingAvgType kind, int fast, int slow, int length, double first, double second, double third, double fourth, bool external = false)
    {
        _multiples = new[] { first, second, third, fourth }; if (_multiples.Any(v => double.IsNaN(v) || double.IsInfinity(v))) throw new ArgumentOutOfRangeException(nameof(first));
        _length = Math.Max(1, length); if (!external) { _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow)); _mean = new(kind, _length); }
    }
    internal static double Typical(double high, double low, double close)
    { var sum = new ExactMeanAccumulator(); sum.Add(high); sum.Add(low); sum.Add(close); return sum.Mean(3); }
    internal static (List<double> Input, List<double> High, List<double> Low, List<double> Close) Inputs(StockData data)
    {
        var input = data.ChainedValues is { Count: > 0 } chained ? chained : Enumerable.Range(0, data.ClosePrices.Count).Select(i => Typical(data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i])).ToList();
        var (high, low) = CalculationsHelper.GetCustomRangeLists(input, data.HighPrices, data.LowPrices);
        return (input, high, low, data.ChainedValues is { Count: > 0 } ? input : data.ClosePrices);
    }
    internal static RocBankValue Range(double high, double low, double olderLow, double olderClose)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var oldLow = ExactVarianceWindow.Units(olderLow); var oldClose = ExactVarianceWindow.Units(olderClose);
        var units = BigInteger.Max(h - oldLow, BigInteger.Max(BigInteger.Abs(h - oldClose), BigInteger.Abs(l - oldClose))); var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum);
    }
    internal static double[] PublishedRanges(IReadOnlyList<double> high, IReadOnlyList<double> low, IReadOnlyList<double> close)
    { var result = new double[close.Count]; for (var i = 0; i < result.Length; i++) result[i] = Range(high[i], low[i], i < 2 ? 0 : low[i - 2], i < 2 ? 0 : close[i - 2]).Publish(); return result; }
    private RocBankValue Deviation(RocBankValue range, bool commit)
    {
        var units = ExactVarianceWindow.Units(range.Mantissa) << range.UpperShift; var full = _ranges.Count == _length; var expired = full ? _ranges.Peek() : BigInteger.Zero;
        var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length); var deviation = default(RocBankValue);
        if (_ranges.Count + 1L >= _length) for (var shift = 0; ; shift += 32) { var value = ExactPopulationDeviation.RootRatio(n * squares - sum * sum, (n * n) << (2 * shift)); if (!double.IsInfinity(value)) { deviation = new(value, shift); break; } }
        if (commit) { _sum = sum; _squares = squares; if (full) _ranges.Dequeue(); _ranges.Enqueue(units); } return deviation;
    }
    internal (double Dev1, double Dev2, double Dev3, double WarningLine, Signal Signal) Next(double high, double low, double close, double input, bool commit, double? externalMean = null, double? externalSlow = null, double? externalFast = null)
    {
        var fast = externalFast.HasValue ? new RocBankValue(externalFast.Value) : _fast!.Next(input, commit); var slow = externalSlow.HasValue ? new RocBankValue(externalSlow.Value) : _slow!.Next(input, commit);
        var spread = new ExactMeanAccumulator(); fast.AddTo(ref spread); slow.AddTo(ref spread, -1); var down = spread.Sign < 0;
        var range = Range(high, low, _olderLow, _olderClose); var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(range, commit); var deviation = Deviation(range, commit);
        RocBankValue Stop(int slot) { var sum = new ExactMeanAccumulator(); sum.AddProduct(deviation.Mantissa, _multiples[slot], down ? 1 : -1); sum.ScaleByPowerOfTwo(deviation.UpperShift); mean.AddTo(ref sum, down ? 1 : -1); sum.Add(input); return RocBankValue.Round(sum); }
        var warning = Stop(0); var first = Stop(1); var second = Stop(2); var third = Stop(3); var change = spread; change.Subtract(_spread);
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _olderLow = _previousLow; _previousLow = low; _olderClose = _previousClose; _previousClose = close; _spread = spread; }
        return (first.Publish(), second.Publish(), third.Publish(), warning.Publish(), signal);
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _mean?.Reset(); _ranges.Clear(); _sum = _squares = default; _previousLow = _olderLow = _previousClose = _olderClose = 0; _spread = default; }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _mean?.Dispose(); }
    // Retain a separately rounded residual in recursive trend state. Publishing each
    // average still rounds once; resetting that residual would change equal-mean direction.
    private sealed class TrendAverage : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<double> _history = new(); private readonly SpreadAverage? _legacy;
        private ExactMeanAccumulator _sum, _weighted; private RocBankValue _high, _low; private long _count;
        internal TrendAverage(MovingAvgType kind, int length) { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _legacy = new(kind, length); }
        internal RocBankValue Next(double value, bool commit)
        {
            if (_legacy is not null) return new(_legacy.Next(new(value), commit).Value);
            var sum = _sum; var weighted = _weighted; var numerator = new ExactMeanAccumulator(); long divisor;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted.Subtract(sum); weighted.Add(value, _length); if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
                if (_kind == MovingAvgType.WeightedMovingAverage) { numerator = weighted; divisor = (long)_length * (_length + 1L) / 2; }
                else { if (_count + 1 >= _length) numerator = sum; divisor = _length; }
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum.Add(value); numerator = sum; divisor = _count + 1; }
            else { var ema = _kind == MovingAvgType.ExponentialMovingAverage; _high.AddTo(ref numerator, _length - 1L); _low.AddTo(ref numerator, _length - 1L); numerator.Add(value, ema ? 2 : 1); divisor = ema ? _length + 1L : _length; }
            var high = RocBankValue.Round(numerator, count: divisor); high.AddTo(ref numerator, -divisor); var low = RocBankValue.Round(numerator, count: divisor);
            if (commit) { _sum = sum; _weighted = weighted; _high = high; _low = low; _count++; if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } } return high;
        }
        internal void Reset() { _history.Clear(); _legacy?.Reset(); _sum = _weighted = default; _high = _low = default; _count = 0; }
        public void Dispose() => _legacy?.Dispose();
    }
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
