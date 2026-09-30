using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class CommoditySelectionWindow : IDisposable
{
    private readonly int _length;
    private readonly BigInteger _point, _rootMargin, _fee;
    private readonly Average[]? _averages;
    private readonly Queue<BigInteger> _history = new();
    private BigInteger _sum, _previousSpread;
    private double _high, _low, _close; private bool _started;
    internal CommoditySelectionWindow(MovingAvgType kind, int length, double pointValue = 50, double margin = 3000, double commission = 10, bool external = false)
    {
        StreamingInputValidation.Finite(pointValue, nameof(pointValue)); StreamingInputValidation.Finite(margin, nameof(margin)); StreamingInputValidation.Finite(commission, nameof(commission));
        if (margin <= 0) throw new ArgumentOutOfRangeException(nameof(margin));
        if (commission == -150) throw new ArgumentOutOfRangeException(nameof(commission)); // NOSONAR: S1244 - Exactly -150 makes the cost denominator zero; nearby finite fees remain supported.
        _point = ExactVarianceWindow.Units(pointValue); _rootMargin = ExactVarianceWindow.Units(Math.Sqrt(margin)); _fee = ExactVarianceWindow.Units(150) + ExactVarianceWindow.Units(commission);
        _length = Math.Max(1, length); if (!external) _averages = Enumerable.Range(0, 5).Select(_ => new Average(kind, _length)).ToArray();
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
    internal (double Value, double Signal, Signal Trade) Next(double high, double low, double close, bool final, double? externalAtr = null, double? externalAdx = null)
    {
        var raw = Raw(high, low, _started ? _high : high, _started ? _low : low, _started ? _close : close);
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _averages![0].Next(raw.Range, final);
        var adx = externalAdx.HasValue ? new RocBankValue(externalAdx.Value) : _averages![4].Next(new(Direction(_averages[1].Next(raw.Plus, final), _averages[2].Next(raw.Minus, final), _averages[3].Next(raw.Range, final))), final);
        var numerator = 100 * _point * Units(atr) * Units(adx); var denominator = _rootMargin * _fee;
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var value = RocBankValue.RoundUnits(numerator, denominator);
        var full = _history.Count == _length; var sum = _sum + value - (full ? _history.Peek() : BigInteger.Zero);
        var mean = RocBankValue.RoundUnits(sum, new BigInteger(Math.Min(_length, _history.Count + 1L)));
        var spread = value - mean;
        var trade = spread.Sign < 0 && spread < _previousSpread ? Signal.StrongBuy : spread.Sign > 0 && spread > _previousSpread ? Signal.StrongSell : spread.Sign < 0 ? Signal.Buy : spread.Sign > 0 ? Signal.Sell : Signal.None;
        if (final) { if (full) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _previousSpread = spread; _high = high; _low = low; _close = close; _started = true; }
        return (ExactMeanAccumulator.UnitRatio(value, BigInteger.One), ExactMeanAccumulator.UnitRatio(mean, BigInteger.One), trade);
    }
    internal static (double[] Atr, double[] Adx) Components(StockData data, List<double> input, List<double> high, List<double> low, MovingAvgType kind, int length)
    {
        var caller = data.CaptureInputSeries(); length = Math.Max(1, length); var ranges = new List<double>(input.Count); var plus = new List<double>(input.Count); var minus = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var old = i > 0 ? i - 1 : i; var raw = Raw(high[i], low[i], high[old], low[old], input[old]); ranges.Add(raw.Range.Publish()); plus.Add(raw.Plus.Publish()); minus.Add(raw.Minus.Publish()); }
        double[] Mean(List<double> values) => ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values).ToArray();
        var atr = Mean(ranges); var positive = Mean(plus); var negative = Mean(minus); var range = Mean(ranges);
        var dx = Enumerable.Range(0, input.Count).Select(i => Direction(new(positive[i]), new(negative[i]), new(range[i]))).ToList(); var adx = Mean(dx); data.RestoreInputSeries(caller); return (atr, adx);
    }
    internal void Reset() { _history.Clear(); _sum = _previousSpread = default; _high = _low = _close = 0; _started = false; if (_averages is not null) foreach (var average in _averages) average.Reset(); }
    public void Dispose() { if (_averages is not null) foreach (var average in _averages) average.Dispose(); }
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
