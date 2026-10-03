using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EnhancedWilliamsWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length; private readonly BigInteger _acceleration;
    private readonly Average? _priceMean, _volumeMean, _signal;
    private readonly LinkedList<(long Index, double Value)> _priceHigh = new(), _priceLow = new(), _volumeHigh = new(), _volumeLow = new();
    private long _index; private double _previousPrice; private BigInteger _previousLine, _previousSpread;
    internal static int MeanLength(int length) { length = Math.Max(1, length); return Math.Max(2, Math.Min(530, length / 2 + length % 2)); }
    internal EnhancedWilliamsWindow(MovingAvgType kind, int length, int signalLength, bool external = false)
    {
        _length = Math.Max(2, length); _acceleration = U(length < 10 ? .25 : length / 32d - .0625);
        if (!external) { _priceMean = new(kind, MeanLength(length)); _volumeMean = new(kind, MeanLength(length)); _signal = new(kind, Math.Max(1, signalLength)); }
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Units(RocBankValue value) => U(value.Mantissa) << value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _length + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    internal (double Line, double SignalLine, Signal Trade, bool Aligned) Next(double price, double volume, bool final,
        double? externalPriceMean = null, double? externalVolumeMean = null, double? externalSignal = null, bool includeSignal = true)
    {
        var priceRange = U(Extreme(_priceHigh, price, true, final)) - U(Extreme(_priceLow, price, false, final));
        var volumeRange = U(Extreme(_volumeHigh, volume, true, final)) - U(Extreme(_volumeLow, volume, false, final));
        var priceMean = externalPriceMean.HasValue ? new RocBankValue(externalPriceMean.Value) : _priceMean!.Next(new(price), final);
        var volumeMean = externalVolumeMean.HasValue ? new RocBankValue(externalVolumeMean.Value) : _volumeMean!.Next(new(volume), final);
        // Keep both normalized ratios rational until the complete branch is formed.
        var pn = priceRange.IsZero ? BigInteger.Zero : 2 * (U(price) - Units(priceMean));
        var vn = volumeRange.IsZero ? BigInteger.Zero : 2 * (U(volume) - Units(volumeMean));
        var pd = priceRange.IsZero ? BigInteger.One : priceRange;
        var vd = volumeRange.IsZero ? BigInteger.One : volumeRange;
        var step = _index == 0 || priceRange.IsZero ? BigInteger.Zero : 2 * (U(price) - U(_previousPrice));
        var factor = step * Unit + _acceleration * pd;
        var aligned = vn.Sign > 0 && (pn.Sign > 0 && price > _previousPrice || pn.Sign < 0 && price < _previousPrice) && !factor.IsZero;
        var denominator = pd * vd;
        var numerator = aligned ? denominator + 50 * pn * vn : 50 * denominator + 25 * pn * (vn + vd);
        var line = Value(RocBankValue.RoundUnits(numerator * Unit, denominator));
        var signal = !includeSignal ? default : externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(line, final);
        var current = Units(line); var spread = current - Units(signal);
        var trade = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell
            : spread.Sign > 0 || _previousLine < -100 * Unit && current > -100 * Unit ? Signal.Buy
            : spread.Sign < 0 || _previousLine > 100 * Unit && current < 100 * Unit ? Signal.Sell : Signal.None;
        if (final) { _index++; _previousPrice = price; _previousLine = current; _previousSpread = spread; }
        return (line.Publish(), signal.Publish(), trade, aligned);
    }
    internal static (double[] PriceMean, double[] VolumeMean, double[] Line, double[] Signal) Components(StockData data, List<double> input, List<double> volume,
        MovingAvgType kind, int length, int signalLength, bool callbacks, bool includeSignal)
    {
        var caller = data.CaptureInputSeries();
        double[] Mean(List<double> values, int period) => (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray();
        var priceMean = Mean(input, MeanLength(length)); var volumeMean = Mean(volume, MeanLength(length)); var line = new double[input.Count];
        using var window = new EnhancedWilliamsWindow(kind, length, signalLength, true);
        for (var i = 0; i < input.Count; i++) line[i] = window.Next(input[i], volume[i], true, priceMean[i], volumeMean[i], includeSignal: false).Line;
        var signal = includeSignal ? Mean(line.ToList(), Math.Max(1, signalLength)) : Array.Empty<double>();
        data.RestoreInputSeries(caller); return (priceMean, volumeMean, line, signal);
    }
    internal void Reset() { _priceHigh.Clear(); _priceLow.Clear(); _volumeHigh.Clear(); _volumeLow.Clear(); _priceMean?.Reset(); _volumeMean?.Reset(); _signal?.Reset(); _index = 0; _previousPrice = 0; _previousLine = _previousSpread = default; }
    public void Dispose() { _priceMean?.Dispose(); _volumeMean?.Dispose(); _signal?.Dispose(); }
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
