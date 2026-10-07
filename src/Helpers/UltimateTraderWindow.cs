using System.Numerics;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// Scores may exceed binary64 even though their normalized sum is bounded.
// Keep candle differences, stochastic endpoints and the six scores exact.
internal sealed class UltimateTraderWindow
{
    private readonly Extrema _ranges, _volumes, _highs, _lows;
    private BigInteger _previous;
    private bool _started;
    internal UltimateTraderWindow(int lookback, int rangeLength)
    {
        _ranges = new(lookback); _volumes = new(lookback);
        _highs = new(rangeLength); _lows = new(rangeLength);
    }

    internal double Next(double open, double high, double low, double close, double volume, bool final)
    {
        var o = ExactVarianceWindow.Units(open); var h = ExactVarianceWindow.Units(high);
        var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close);
        var v = ExactVarianceWindow.Units(volume);
        var prior = _started ? _previous : c;
        var tr = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - prior), BigInteger.Abs(l - prior)));
        var trBounds = _ranges.Next(tr, final); var volumeBounds = _volumes.Next(v, final);
        var top = _highs.Next(h, final).High; var bottom = _lows.Next(l, final).Low;
        var change = c - (_started ? _previous : BigInteger.Zero);
        var range = h - l; var extent = top - bottom;
        Fraction Ratio(BigInteger numerator, BigInteger denominator) => denominator.IsZero ? 0 : new Fraction(100 * numerator, denominator);
        var body = Ratio(c - o, range);
        var position = range.IsZero ? (Fraction)0 : 2 * Ratio(c - l, range) - 100;
        var rollingPosition = change.IsZero || extent.IsZero ? (Fraction)0 : 2 * Ratio(c - bottom, extent) - 100;
        var momentum = Ratio(change, extent);
        var trueRange = change.Sign * Ratio(tr - trBounds.Low, trBounds.High - trBounds.Low);
        var size = change.Sign * Ratio(v - volumeBounds.Low, volumeBounds.High - volumeBounds.Low);
        var total = body.Abs() + position.Abs() + rollingPosition.Abs() + momentum.Abs() + trueRange.Abs() + size.Abs();
        var result = total.Sign == 0 ? 0 : (100 * (body + position + rollingPosition + momentum + trueRange + size) / total).Publish();
        if (final) { _previous = c; _started = true; }
        return result;
    }

    internal void Reset()
    {
        _ranges.Reset(); _volumes.Reset(); _highs.Reset(); _lows.Reset();
        _previous = default; _started = false;
    }

    internal static double[] Raw(StockData data, int lookback, int rangeLength)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in series) Streaming.StreamingInputValidation.Finite(value, nameof(data));
        var window = new UltimateTraderWindow(lookback, rangeLength);
        var result = new double[prices.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = window.Next(data.OpenPrices[i], data.HighPrices[i], data.LowPrices[i], prices[i], data.Volumes[i], true);
        return result;
    }

    internal static List<double> Smooth(StockData data, MovingAvgType kind, int length, List<double> values, bool callbacks)
    {
        length = Math.Max(1, length);
        var replacement = callbacks ? Builder.Compute.ComponentAverage.Take(values.ToArray(), length) : null;
        if (replacement is not null) return Enumerable.Range(0, values.Count).Select(i => i < replacement.Count ? replacement[i] : 0).ToList();
        if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, length, values);
        using var average = new Average(kind, length);
        return values.Select(v => average.Next(v, true)).ToList();
    }

    // Preserve rounding at each published smoothing stage and each recursive
    // feedback step, without allocating a period-sized buffer on construction.
    internal sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<double> _history = new();
        private readonly Streaming.IMovingAverageSmoother? _fallback;
        private ExactMeanAccumulator _sum, _weighted;
        private double _previous;
        private long _count;
        internal Average(MovingAvgType kind, int length)
        {
            _kind = kind; _length = Math.Max(1, length);
            if (!StrengthWindow.Supports(kind)) _fallback = Streaming.MovingAverageSmootherFactory.Create(kind, _length);
        }
        internal double Next(double value, bool final)
        {
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; double result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted.Subtract(sum); weighted.Add(value, _length);
                if (_history.Count == _length) sum.Add(_history.Peek(), -1);
                sum.Add(value);
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                    : _count < _length - 1 ? 0 : sum.Mean(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum.Add(value); result = sum.Mean(_count + 1); }
            else
            {
                var next = new ExactMeanAccumulator(); var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                next.Add(_previous, _length - 1L); next.Add(value, ema ? 2 : 1);
                result = next.Mean(ema ? _length + 1L : _length);
            }
            if (final)
            {
                if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _previous = 0; _count = 0; _fallback?.Reset(); }
        public void Dispose() { Reset(); _fallback?.Dispose(); }
    }

    // Monotone queues allocate only for observed bars. A preview excludes the
    // expiring head without changing either queue or its next sequence number.
    private sealed class Extrema
    {
        private readonly int _length;
        private readonly LinkedList<(long Index, BigInteger Value)> _min = new(), _max = new();
        private long _index;
        internal Extrema(int length) => _length = Math.Max(1, length);
        internal (BigInteger Low, BigInteger High) Next(BigInteger value, bool final)
        {
            var cutoff = _index - _length;
            var low = _min.First; while (low is not null && low.Value.Index <= cutoff) low = low.Next;
            var high = _max.First; while (high is not null && high.Value.Index <= cutoff) high = high.Next;
            var result = (low is null ? value : BigInteger.Min(value, low.Value.Value),
                high is null ? value : BigInteger.Max(value, high.Value.Value));
            if (final)
            {
                while (_min.First is { } first && first.Value.Index <= cutoff) _min.RemoveFirst();
                while (_max.First is { } first && first.Value.Index <= cutoff) _max.RemoveFirst();
                while (_min.Last is { } last && last.Value.Value >= value) _min.RemoveLast();
                while (_max.Last is { } last && last.Value.Value <= value) _max.RemoveLast();
                _min.AddLast((_index, value)); _max.AddLast((_index, value)); _index++;
            }
            return result;
        }
        internal void Reset() { _min.Clear(); _max.Clear(); _index = 0; }
    }
}
