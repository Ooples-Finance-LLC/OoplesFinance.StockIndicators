using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Partial-window Pearson correlation against consecutive time, then its rounded
// square and the selected average. Local indices avoid an ever-growing clock.
internal sealed class ChandeKrollWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<BigInteger> _history = new();
    private BigInteger _sum, _squares, _weighted, _previousSlope;
    private double _previous;
    private readonly Average? _average;
    internal ChandeKrollWindow(MovingAvgType kind, int length, int smoothLength, bool external = false)
    { _length = Math.Max(1, length); if (!external) _average = new(kind, Math.Max(1, smoothLength)); }
    internal (double Value, double Raw, Signal Signal) Next(double price, bool final, double? external = null)
    {
        var current = ExactVarianceWindow.Units(price); var full = _history.Count == _length; var old = full ? _history.Peek() : BigInteger.Zero;
        var sum = _sum + current - old; var squares = _squares + current * current - old * old;
        var count = Math.Min(_length, _history.Count + 1L); var n = new BigInteger(count);
        var weighted = full ? _weighted - _sum + old + (_length - 1L) * current : _weighted + _history.Count * current;
        var centered = n * squares - sum * sum; var covariance = 2 * weighted - (n - 1) * sum;
        var correlation = count < 2 || centered.IsZero ? 0 : ExactPopulationDeviation.RootRatio((3 * covariance * covariance) << 2148, (n * n - 1) * centered);
        var raw = correlation * correlation; var value = external ?? _average!.Next(raw, final);
        var slope = ExactVarianceWindow.Units(value) - ExactVarianceWindow.Units(_previous);
        var signal = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _history.Dequeue(); _history.Enqueue(current); _sum = sum; _squares = squares; _weighted = weighted;
            _previous = value; _previousSlope = slope;
        }
        return (value, raw, signal);
    }
    internal static double[] Component(StockData data, List<double> input, MovingAvgType kind, int length, int smoothLength)
    {
        var caller = data.CaptureInputSeries(); using var window = new ChandeKrollWindow(kind, length, smoothLength, true);
        var values = input.Select(p => window.Next(p, true, 0).Raw).ToList(); var period = Math.Max(1, smoothLength);
        var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray(); data.RestoreInputSeries(caller); return result;
    }
    internal void Reset() { _history.Clear(); _sum = _squares = _weighted = _previousSlope = default; _previous = 0; _average?.Reset(); }
    public void Dispose() => _average?.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal double Next(double value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(new(value), final).Publish();
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); weighted.Add(value, _length);
            if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2) : _history.Count < _length - 1 ? 0 : sum.Mean(_length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
