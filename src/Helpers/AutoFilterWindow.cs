using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Regress price on its deviation-gated step using exact covariance/variance.
// Selected means round independently; center the regression before publishing.
internal sealed class AutoFilterWindow : IDisposable
{
    private readonly int _length; private readonly Average? _priceMean, _stepMean;
    private readonly Queue<(BigInteger Price, BigInteger Step)> _history = new();
    private BigInteger _sumY, _sumX, _yy, _xx, _xy, _spread;
    private double _previousStep; private bool _started;
    internal AutoFilterWindow(MovingAvgType kind, int length, bool external = false)
    { _length = Math.Max(1, length); if (!external) { _priceMean = new(kind, _length); _stepMean = new(kind, _length); } }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal (double Line, Signal Signal, double Step) Next(double price, bool commit, double? externalPriceMean = null, double? externalStepMean = null)
    {
        var y = ExactVarianceWindow.Units(price); var full = _history.Count == _length;
        var old = full ? _history.Peek() : (Price: BigInteger.Zero, Step: BigInteger.Zero);
        var sumY = _sumY + y - old.Price; var yy = _yy + y * y - old.Price * old.Price;
        var ready = _history.Count >= _length - 1;
        var deviation = ready ? ExactPopulationDeviation.RootRatio(_length * yy - sumY * sumY, (BigInteger)_length * _length) : 0;
        var previous = _started ? _previousStep : price;
        var step = price > previous + deviation || price < previous - deviation ? price : previous;
        var x = ExactVarianceWindow.Units(step); var sumX = _sumX + x - old.Step;
        var xx = _xx + x * x - old.Step * old.Step; var xy = _xy + x * y - old.Step * old.Price;
        var variance = _length * xx - sumX * sumX; var covariance = _length * xy - sumX * sumY;
        var meanY = Units(externalPriceMean.HasValue ? new RocBankValue(externalPriceMean.Value) : _priceMean!.Next(new(price), commit));
        var meanX = Units(externalStepMean.HasValue ? new RocBankValue(externalStepMean.Value) : _stepMean!.Next(new(step), commit));
        var line = !ready || variance.IsZero ? meanY : RocBankValue.RoundUnits(meanY * variance + covariance * (x - meanX), variance);
        var spread = y - line; var change = spread - _spread;
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit)
        {
            if (full) _history.Dequeue(); _history.Enqueue((y, x)); _sumY = sumY; _sumX = sumX; _yy = yy; _xx = xx; _xy = xy;
            _previousStep = step; _started = true; _spread = spread;
        }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal, step);
    }
    internal static double[][] Components(StockData data, List<double> input, MovingAvgType kind, int length)
    {
        length = Math.Max(1, length); var caller = data.CaptureInputSeries();
        double[] Mean(List<double> values) { var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values).ToArray(); data.RestoreInputSeries(caller); return result; }
        var priceMean = Mean(input); using var window = new AutoFilterWindow(kind, length, true);
        var steps = input.Select(price => window.Next(price, true, 0, 0).Step).ToList(); var stepMean = Mean(steps); return new[] { priceMean, stepMean };
    }
    internal void Reset() { _history.Clear(); _sumY = _sumX = _yy = _xx = _xy = _spread = BigInteger.Zero; _previousStep = 0; _started = false; _priceMean?.Reset(); _stepMean?.Reset(); }
    public void Dispose() { _priceMean?.Dispose(); _stepMean?.Dispose(); }
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
