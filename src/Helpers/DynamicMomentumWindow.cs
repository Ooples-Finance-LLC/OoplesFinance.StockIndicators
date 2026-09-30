using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DynamicMomentumWindow : IDisposable
{
    private readonly int _deviationLength, _baseline, _minimum, _maximum;
    private readonly Average _average;
    private readonly Queue<BigInteger> _prices = new();
    private BigInteger _priceSum, _priceSquares, _previousHistogram;
    private readonly Prefix _gains, _losses, _lines;
    private double _previousPrice, _previousLine; private bool _started;
    internal DynamicMomentumWindow(MovingAvgType kind, int deviationLength, int averageLength, int baseline, int minimum, int maximum)
    {
        _deviationLength = Math.Max(1, deviationLength); _baseline = Math.Max(1, baseline); _minimum = Math.Max(1, minimum); _maximum = Math.Max(_minimum, maximum);
        _average = new(kind, Math.Max(1, averageLength)); _gains = new(_maximum); _losses = new(_maximum); _lines = new(_maximum);
    }
    internal double Deviation(double price, bool final)
    {
        var current = ExactVarianceWindow.Units(price); var full = _prices.Count == _deviationLength; var expired = full ? _prices.Peek() : BigInteger.Zero;
        var sum = _priceSum + current - expired; var squares = _priceSquares + current * current - expired * expired;
        var result = _prices.Count < _deviationLength - 1 ? 0 : ExactPopulationDeviation.RootRatio(_deviationLength * squares - sum * sum, (BigInteger)_deviationLength * _deviationLength);
        if (final) { if (full) _prices.Dequeue(); _prices.Enqueue(current); _priceSum = sum; _priceSquares = squares; }
        return result;
    }
    internal (double Line, double SignalLine, double Histogram, Signal Trade, int Period) Next(double price, bool final, double? externalAverage = null)
    {
        var deviation = Deviation(price, final);
        var average = externalAverage ?? _average.Next(new(deviation), final).Publish();
        var period = DynamicMomentumPeriod.Calculate(deviation, average, _baseline, _minimum, _maximum);
        var change = _started ? RocBankValue.RoundUnits(ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(_previousPrice), BigInteger.One) : BigInteger.Zero;
        var gain = BigInteger.Max(change, BigInteger.Zero); var loss = BigInteger.Max(-change, BigInteger.Zero);
        var gains = _gains.Sum(gain, period); var losses = _losses.Sum(loss, period);
        // The common divisor of the gain/loss means cancels. Dividing first
        // can discard a subnormal loss and spuriously return 100.
        var line = losses.IsZero ? 100 : ExactMeanAccumulator.UnitRatio(100 * gains << 1074, gains + losses);
        var lineUnits = ExactVarianceWindow.Units(line);
        var count = Math.Min((long)period, _lines.Count + 1L);
        var signal = ExactMeanAccumulator.UnitRatio(_lines.Sum(lineUnits, period), new BigInteger(count));
        var histogram = lineUnits - ExactVarianceWindow.Units(signal);
        var trade = histogram.Sign > 0 && histogram > _previousHistogram ? Signal.StrongBuy : histogram.Sign < 0 && histogram < _previousHistogram ? Signal.StrongSell
            : histogram.Sign > 0 || _previousLine < 30 && line > 30 ? Signal.Buy : histogram.Sign < 0 || _previousLine > 70 && line < 70 ? Signal.Sell : Signal.None;
        if (final) { _gains.Add(gain); _losses.Add(loss); _lines.Add(lineUnits); _previousPrice = price; _previousLine = line; _previousHistogram = histogram; _started = true; }
        return (line, signal, ExactMeanAccumulator.UnitRatio(histogram, BigInteger.One), trade, period);
    }
    internal static double[] Components(StockData data, List<double> input, MovingAvgType kind, int deviationLength, int averageLength, bool callbacks)
    {
        var caller = data.CaptureInputSeries(); averageLength = Math.Max(1, averageLength);
        using var window = new DynamicMomentumWindow(MovingAvgType.SimpleMovingAverage, deviationLength, 1, 1, 1, 1);
        var deviations = input.Select(v => window.Deviation(v, true)).ToList();
        var mean = (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(deviations), averageLength)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, averageLength, deviations).ToArray();
        data.RestoreInputSeries(caller); return mean;
    }
    internal void Reset() { _prices.Clear(); _priceSum = _priceSquares = _previousHistogram = default; _gains.Reset(); _losses.Reset(); _lines.Reset(); _average.Reset(); _previousPrice = _previousLine = 0; _started = false; }
    public void Dispose() => _average.Dispose();
    // Cumulative exact sums allow a changing lookback to query retained history
    // in constant time. Compaction is amortized; no allocation uses the period.
    private sealed class Prefix
    {
        private readonly int _capacity; private readonly List<BigInteger> _prefix = new() { BigInteger.Zero }; private int _start;
        internal Prefix(int capacity) => _capacity = capacity;
        internal int Count => _prefix.Count - _start - 1;
        internal BigInteger Sum(BigInteger current, int period) => _prefix[_prefix.Count - 1] + current - _prefix[Math.Max(_start, _prefix.Count - period)];
        internal void Add(BigInteger value)
        {
            _prefix.Add(_prefix[_prefix.Count - 1] + value);
            if (_prefix.Count - (long)_start > _capacity + 1L) _start++;
            if (_start >= 1024 && _start >= _prefix.Count / 2) { _prefix.RemoveRange(0, _start); _start = 0; }
        }
        internal void Reset() { _prefix.Clear(); _prefix.Add(BigInteger.Zero); _start = 0; }
    }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(value, final);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count < _length - 1 ? default : RocBankValue.Round(sum, count: _length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
