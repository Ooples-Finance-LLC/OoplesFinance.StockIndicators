using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ErgodicSelectionWindow : IDisposable
{
    private readonly int _length, _smooth;
    private readonly BigInteger _point, _root;
    private readonly Average[]? _direction;
    private readonly Average? _signal;
    private double _high, _low, _close; private bool _started;
    private BigInteger _previousAdx, _previousSignal, _previousSlope;
    internal ErgodicSelectionWindow(MovingAvgType kind, int length, int smoothLength, double pointValue, bool external = false)
    {
        StreamingInputValidation.Finite(pointValue, nameof(pointValue));
        _length = Math.Max(1, length); _smooth = Math.Max(1, smoothLength);
        _point = ExactVarianceWindow.Units(pointValue); _root = ExactVarianceWindow.Units(Math.Sqrt(_length));
        if (!external) _direction = Enumerable.Range(0, 4).Select(_ => new Average(kind, _length)).ToArray();
        if (!external || StrengthWindow.Supports(kind)) _signal = new(kind, _smooth);
    }
    private static RocBankValue Rounded(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static (RocBankValue Range, RocBankValue Plus, RocBankValue Minus) Raw(double high, double low, double previousHigh, double previousLow, double previousClose)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(previousClose);
        var up = h - ExactVarianceWindow.Units(previousHigh); var down = ExactVarianceWindow.Units(previousLow) - l;
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - c), BigInteger.Abs(l - c)));
        return (Rounded(range), up.Sign > 0 && up > down ? Rounded(up) : default, down.Sign > 0 && down > up ? Rounded(down) : default);
    }
    private static double Percent(RocBankValue movement, RocBankValue range)
    {
        if (range.Mantissa == 0) return 0;
        var numerator = new ExactMeanAccumulator(); movement.AddTo(ref numerator, 100); var denominator = new ExactMeanAccumulator(); range.AddTo(ref denominator);
        return Math.Max(0, Math.Min(100, numerator.Ratio(denominator)));
    }
    private static double Direction(RocBankValue plus, RocBankValue minus, RocBankValue range)
    {
        var positive = Percent(plus, range); var negative = Percent(minus, range);
        var numerator = new ExactMeanAccumulator(); numerator.Add(positive); numerator.Add(negative, -1);
        var denominator = new ExactMeanAccumulator(); denominator.Add(positive); denominator.Add(negative);
        return denominator.IsExactlyZero ? 0 : Math.Min(100, Math.Abs(numerator.Ratio(denominator)) * 100);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double close, bool final,
        double? externalAdx = null, double? externalSignal = null, bool includeSignal = true)
    {
        var raw = Raw(high, low, _started ? _high : high, _started ? _low : low, _started ? _close : close);
        var adx = externalAdx.HasValue ? new RocBankValue(externalAdx.Value) : _direction![3].Next(new(Direction(
            _direction[0].Next(raw.Plus, final), _direction[1].Next(raw.Minus, final), _direction[2].Next(raw.Range, final))), final);
        var currentAdx = Units(adx);
        // Keep the complete price-normalized quotient: neither the scale nor true range needs to fit a published double.
        var numerator = 100 * _point * (currentAdx + _previousAdx) * Units(raw.Range);
        var denominator = 2 * _root * (150L + _smooth) * _length;
        var line = close > 0 ? RocBankValue.RoundUnits(numerator, denominator * ExactVarianceWindow.Units(close)) : BigInteger.Zero;
        var signal = !includeSignal ? default : externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(Rounded(line), final);
        var current = Units(signal); var slope = current - _previousSignal;
        var trade = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _high = high; _low = low; _close = close; _started = true; _previousAdx = currentAdx; _previousSignal = current; _previousSlope = slope; }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal.Publish(), trade);
    }
    internal static (double[] Adx, double[] Line, double[] Signal) Components(StockData data, List<double> input, List<double> high, List<double> low,
        MovingAvgType kind, int length, int smoothLength, double pointValue, bool callbacks, bool includeSignal)
    {
        var caller = data.CaptureInputSeries(); length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        using var window = new ErgodicSelectionWindow(kind, length, smoothLength, pointValue, true);
        var ranges = new List<double>(input.Count); var plus = new List<double>(input.Count); var minus = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var old = i > 0 ? i - 1 : i; var raw = Raw(high[i], low[i], high[old], low[old], input[old]); ranges.Add(raw.Range.Publish()); plus.Add(raw.Plus.Publish()); minus.Add(raw.Minus.Publish()); }
        double[] Mean(List<double> values, int period) => (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray();
        var p = Mean(plus, length); var m = Mean(minus, length); var tr = Mean(ranges, length);
        var dx = Enumerable.Range(0, input.Count).Select(i => Direction(new(p[i]), new(m[i]), new(tr[i]))).ToList(); var adx = Mean(dx, length);
        var line = new double[input.Count]; var exactSignal = new double[input.Count]; var supported = StrengthWindow.Supports(kind);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, adx[i], includeSignal: includeSignal && supported); line[i] = point.Line; exactSignal[i] = point.SignalLine; }
        var signal = !includeSignal ? Array.Empty<double>() : (callbacks ? ComponentAverage.Take(line, smoothLength)?.ToArray() : null)
            ?? (supported ? exactSignal : CalculationsHelper.GetMovingAverageList(data, kind, smoothLength, line.ToList()).ToArray());
        data.RestoreInputSeries(caller); return (adx, line, signal);
    }
    internal void Reset() { _high = _low = _close = 0; _started = false; _previousAdx = _previousSignal = _previousSlope = default; if (_direction is not null) foreach (var mean in _direction) mean.Reset(); _signal?.Reset(); }
    public void Dispose() { if (_direction is not null) foreach (var mean in _direction) mean.Dispose(); _signal?.Dispose(); }
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
