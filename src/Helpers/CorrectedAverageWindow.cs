using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Preserve the rounded deviation, displacement, square, gain and blend stages.
// Extended binary64 intermediates avoid overflow before a bounded correction.
internal sealed class CorrectedAverageWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length; private readonly Average? _average;
    private readonly Queue<BigInteger> _history = new();
    private BigInteger _sum, _squares, _previous, _previousPrice; private int _count;
    internal CorrectedAverageWindow(MovingAvgType kind, int length, bool external = false)
    { _length = Math.Max(1, length); if (!external) _average = new(kind, _length); }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal (double Value, Signal Signal, double Gain) Next(double price, bool commit, double? externalMean = null)
    {
        var current = ExactVarianceWindow.Units(price); var full = _history.Count == _length;
        var expired = full ? _history.Peek() : BigInteger.Zero;
        var sum = _sum + current - expired; var squares = _squares + current * current - expired * expired;
        var ready = _history.Count >= _length - 1;
        var deviation = ready ? ExactPopulationDeviation.RootRatio(_length * squares - sum * sum, (BigInteger)_length * _length) : 0;
        var sigma = ExactVarianceWindow.Units(deviation); var variance = RocBankValue.RoundUnits(sigma * sigma, Unit);
        var mean = Units(externalMean.HasValue ? new RocBankValue(externalMean.Value) : _average!.Next(new(price), commit));
        var previous = _count == 0 ? mean : _previous;
        var displacement = RocBankValue.RoundUnits(mean - previous, BigInteger.One);
        var distance = RocBankValue.RoundUnits(displacement * displacement, Unit);
        var gain = variance.IsZero ? 1 : distance <= variance ? 0 : 1 - ExactMeanAccumulator.UnitRatio(variance * Unit, distance);
        var step = RocBankValue.RoundUnits(displacement * ExactVarianceWindow.Units(gain), Unit);
        var line = _count < _length ? mean : RocBankValue.RoundUnits(previous + step, BigInteger.One);
        var spread = current - line; var priorSpread = _previousPrice - previous; var change = spread - priorSpread;
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit)
        {
            if (full) _history.Dequeue(); _history.Enqueue(current); _sum = sum; _squares = squares;
            _previous = line; _previousPrice = current; if (_count < _length) _count++;
        }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal, gain);
    }
    internal static double[] Components(StockData data, List<double> input, MovingAvgType kind, int length)
    {
        var caller = data.CaptureInputSeries(); length = Math.Max(1, length);
        var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length)?.ToArray()
            ?? CalculationsHelper.GetMovingAverageList(data, kind, length, input).ToArray();
        data.RestoreInputSeries(caller); return result;
    }
    internal void Reset() { _history.Clear(); _sum = _squares = _previous = _previousPrice = default; _count = 0; _average?.Reset(); }
    public void Dispose() => _average?.Dispose();
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
