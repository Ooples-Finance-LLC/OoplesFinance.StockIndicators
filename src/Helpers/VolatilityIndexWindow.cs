using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Both VIDYA aliases normalize a population deviation by its selected mean.
// Keep the gain exact through the recursive blend, which may extrapolate.
internal sealed class VolatilityIndexWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length; private readonly BigInteger _alpha1, _alpha2;
    private readonly Queue<BigInteger> _history = new(); private BigInteger _sum, _squares;
    private readonly Average? _mean;
    private BigInteger _first, _second, _bullish, _bearish; private bool _started;
    internal VolatilityIndexWindow(MovingAvgType kind, int length, double alpha1, double alpha2, bool external = false)
    {
        StreamingInputValidation.Finite(alpha1, nameof(alpha1)); StreamingInputValidation.Finite(alpha2, nameof(alpha2));
        if (alpha1 < 0) throw new ArgumentOutOfRangeException(nameof(alpha1)); if (alpha2 < 0) throw new ArgumentOutOfRangeException(nameof(alpha2));
        _alpha1 = ExactVarianceWindow.Units(alpha1); _alpha2 = ExactVarianceWindow.Units(alpha2); _length = Math.Max(1, length); if (!external) _mean = new(kind, _length);
    }
    private static BigInteger Blend(BigInteger price, BigInteger previous, BigInteger deviation, BigInteger mean, BigInteger alpha)
    {
        if (mean.IsZero) return previous;
        var denominator = mean * Unit; var numerator = previous * denominator + alpha * deviation * (price - previous);
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        return RocBankValue.RoundUnits(numerator, denominator);
    }
    internal (double First, double Second, double Deviation, Signal Signal) Next(double price, bool final, double? externalMean = null)
    {
        var current = ExactVarianceWindow.Units(price); var full = _history.Count == _length; var old = full ? _history.Peek() : BigInteger.Zero;
        var sum = _sum + current - old; var squares = _squares + current * current - old * old;
        var deviation = _history.Count < _length - 1 ? 0 : ExactPopulationDeviation.RootRatio(_length * squares - sum * sum, (BigInteger)_length * _length);
        var average = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(new(deviation), final);
        var mean = ExactVarianceWindow.Units(average.Mantissa) << average.UpperShift; var sigma = ExactVarianceWindow.Units(deviation);
        var first = Blend(current, _started ? _first : current, sigma, mean, _alpha1); var second = Blend(current, _started ? _second : current, sigma, mean, _alpha2);
        var bullish = current - BigInteger.Max(first, second); var bearish = current - BigInteger.Min(first, second);
        var signal = bullish.Sign > 0 && bullish > _bullish ? Signal.StrongBuy : bearish.Sign < 0 && bearish < _bearish ? Signal.StrongSell : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { if (full) _history.Dequeue(); _history.Enqueue(current); _sum = sum; _squares = squares; _first = first; _second = second; _bullish = bullish; _bearish = bearish; _started = true; }
        return (ExactMeanAccumulator.UnitRatio(first, BigInteger.One), ExactMeanAccumulator.UnitRatio(second, BigInteger.One), deviation, signal);
    }
    internal static double[] Component(StockData data, List<double> input, MovingAvgType kind, int length)
    {
        var caller = data.CaptureInputSeries(); using var raw = new VolatilityIndexWindow(kind, length, 0, 0, true); var deviations = input.Select(p => raw.Next(p, true, 0).Deviation).ToList();
        var period = Math.Max(1, length); var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(deviations), period)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, period, deviations).ToArray(); data.RestoreInputSeries(caller); return result;
    }
    internal void Reset() { _history.Clear(); _sum = _squares = _first = _second = _bullish = _bearish = default; _started = false; _mean?.Reset(); }
    public void Dispose() => _mean?.Dispose();
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
