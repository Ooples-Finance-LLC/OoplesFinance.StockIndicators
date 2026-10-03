using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Keep price differences and deviation-weighted products exact until their ratios.
// The published composite is bounded; its signal uses the available trailing bars.
internal sealed class ChandeCompositeWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly Leg[] _legs;
    private readonly int _length, _smoothLength;
    private readonly Queue<double> _signalHistory = new();
    private ExactMeanAccumulator _signalSum;
    private BigInteger _previousPrice, _previousSpread;
    private double _previousLine;
    private bool _started;
    internal static bool Supports(MovingAvgType kind) => StrengthWindow.Supports(kind) || kind == MovingAvgType.DoubleExponentialMovingAverage;
    internal ChandeCompositeWindow(MovingAvgType kind, int length1, int length2, int length3, int smoothLength, bool external = false)
    {
        _length = Math.Max(1, length1); _smoothLength = Math.Max(1, smoothLength);
        _legs = new[] { length1, length2, length3 }.Select(n => new Leg(kind, Math.Max(1, n), _smoothLength, external)).ToArray();
    }
    internal (double Line, double SignalLine, Signal Signal, double Ratio1, double Ratio2, double Ratio3) Next(double price, bool final, double? external1 = null, double? external2 = null, double? external3 = null)
    {
        var current = ExactVarianceWindow.Units(price); var change = _started ? current - _previousPrice : BigInteger.Zero;
        var numerator = BigInteger.Zero; var denominator = BigInteger.Zero; double ratio1 = 0, ratio2 = 0, ratio3 = 0;
        for (var i = 0; i < 3; i++)
        {
            var point = _legs[i].Next(current, change, final, i == 0 ? external1 : i == 1 ? external2 : external3); if (i == 0) ratio1 = point.Ratio; else if (i == 1) ratio2 = point.Ratio; else ratio3 = point.Ratio;
            var weight = ExactVarianceWindow.Units(point.Weight); denominator += weight;
            numerator += weight * ExactVarianceWindow.Units(point.Momentum);
        }
        var composite = denominator.IsZero ? 0 : Math.Max(-100, Math.Min(100, ExactMeanAccumulator.UnitRatio(numerator, denominator)));
        var sum = _signalSum; if (_signalHistory.Count == _length) sum.Add(_signalHistory.Peek(), -1); sum.Add(composite);
        var signalLine = sum.Mean(Math.Min(_length, _signalHistory.Count + 1L));
        var blend = new ExactMeanAccumulator(); blend.Add(_previousLine, _smoothLength - 1); blend.Add(composite, 2);
        var line = blend.Mean(_smoothLength + 1L); var spread = ExactVarianceWindow.Units(line) - ExactVarianceWindow.Units(signalLine);
        var signal = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell
            : spread.Sign > 0 || _previousLine < -70 && line > -70 ? Signal.Buy : spread.Sign < 0 || _previousLine > 70 && line < 70 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_signalHistory.Count == _length) _signalHistory.Dequeue(); _signalHistory.Enqueue(composite); _signalSum = sum;
            _previousPrice = current; _previousLine = line; _previousSpread = spread; _started = true;
        }
        return (line, signalLine, signal, ratio1, ratio2, ratio3);
    }
    internal static double[][] Components(StockData data, List<double> input, MovingAvgType kind, int length1, int length2, int length3, int smoothLength)
    {
        var caller = data.CaptureInputSeries(); using var raw = new ChandeCompositeWindow(kind, length1, length2, length3, smoothLength, true);
        var ratios = new[] { new List<double>(input.Count), new List<double>(input.Count), new List<double>(input.Count) };
        foreach (var price in input) { var point = raw.Next(price, true, 0, 0, 0); ratios[0].Add(point.Ratio1); ratios[1].Add(point.Ratio2); ratios[2].Add(point.Ratio3); }
        var result = new double[3][];
        for (var i = 0; i < 3; i++) { result[i] = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ratios[i]), Math.Max(1, smoothLength))?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, smoothLength), ratios[i]).ToArray(); data.RestoreInputSeries(caller); }
        return result;
    }
    internal void Reset() { foreach (var leg in _legs) leg.Reset(); _signalHistory.Clear(); _signalSum = default; _previousPrice = _previousSpread = default; _previousLine = 0; _started = false; }
    public void Dispose() { foreach (var leg in _legs) leg.Dispose(); }
    private sealed class Leg : IDisposable
    {
        private readonly int _length;
        private readonly Queue<(BigInteger Price, BigInteger Change)> _history = new();
        private BigInteger _sum, _squares, _change, _travel;
        private readonly Average? _average;
        internal Leg(MovingAvgType kind, int length, int smoothLength, bool external) { _length = length; if (!external) _average = new(kind, smoothLength); }
        internal (double Ratio, double Weight, double Momentum) Next(BigInteger price, BigInteger change, bool final, double? external)
        {
            var full = _history.Count == _length; var old = full ? _history.Peek() : (Price: BigInteger.Zero, Change: BigInteger.Zero);
            var sum = _sum + price - old.Price; var squares = _squares + price * price - old.Price * old.Price;
            var signed = _change + change - old.Change; var travel = _travel + BigInteger.Abs(change) - BigInteger.Abs(old.Change);
            var ratio = travel.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(100 * signed * Unit, travel);
            var weight = _history.Count < _length - 1 ? 0 : ExactPopulationDeviation.RootRatio(_length * squares - sum * sum, (BigInteger)_length * _length);
            var momentum = external ?? _average!.Next(ratio, final);
            if (final) { if (full) _history.Dequeue(); _history.Enqueue((price, change)); _sum = sum; _squares = squares; _change = signed; _travel = travel; }
            return (ratio, weight, momentum);
        }
        internal void Reset() { _history.Clear(); _sum = _squares = _change = _travel = default; _average?.Reset(); }
        public void Dispose() => _average?.Dispose();
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _first, _second; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        {
            _kind = kind; _length = length;
            if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod or MovingAvgType.DoubleExponentialMovingAverage)
            { _first = new(kind == MovingAvgType.DoubleExponentialMovingAverage ? MovingAvgType.ExponentialMovingAverage : kind, length, 1); if (kind == MovingAvgType.DoubleExponentialMovingAverage) _second = new(MovingAvgType.ExponentialMovingAverage, length, 1); }
            else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length);
        }
        internal double Next(double value, bool final)
        {
            if (_first is not null) { var first = _first.Next(new(value), final); if (_second is null) return first.Publish(); var second = _second.Next(first, final); var result = new ExactMeanAccumulator(); first.AddTo(ref result, 2); second.AddTo(ref result, -1); return result.Mean(1); }
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); weighted.Add(value, _length);
            if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
            var output = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2) : _history.Count < _length - 1 ? 0 : sum.Mean(_length);
            if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _weighted = weighted; } return output;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _first?.Reset(); _second?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _first?.Dispose(); _second?.Dispose(); _fallback?.Dispose(); }
    }
}
