using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class JrcWindow : IDisposable
{
    private readonly int _length, _scale, _aggregation, _longLength;
    private readonly double _logScale;
    private readonly Extrema _shortRange, _longRange;
    private readonly Queue<double> _shortPrices = new(), _longPrices = new();
    private readonly Queue<BigInteger> _ranges = new();
    private readonly Mean? _line, _signal;
    private BigInteger _sum, _previousSpread;
    private long _index;
    internal static int BoundedProduct(int left, int right) => (int)Math.Max(2L, Math.Min(530L, (long)left * right));
    internal JrcWindow(MovingAvgType kind, int length, int scale, int smoothLength, bool external = false)
    {
        _length = Math.Max(1, length); _scale = Math.Max(1, scale);
        _aggregation = BoundedProduct(_length, _scale - 1); _longLength = BoundedProduct(_length, _scale);
        _logScale = Math.Log(_scale); _shortRange = new(_length); _longRange = new(_longLength);
        if (!external) { _line = new(kind, smoothLength); _signal = new(kind, smoothLength); }
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    internal static double LogRatio(BigInteger numerator, BigInteger denominator)
    {
        if (numerator == denominator) return 0;
        if (2 * numerator >= denominator && numerator <= 2 * denominator)
        {
            var relative = ExactMeanAccumulator.UnitRatio((numerator - denominator) << 1074, denominator);
            var argument = 1 + relative;
            // Correct the rounded argument, retaining a ratio close to one.
            if (argument == 1) return relative;
            var top = new ExactMeanAccumulator(); top.AddProduct(Math.Log(argument), relative);
            var bottom = new ExactMeanAccumulator(); bottom.Add(argument - 1);
            return top.Ratio(bottom);
        }
        var exponent = (numerator.ToByteArray().Length - denominator.ToByteArray().Length) * 8;
        if (exponent >= 0) denominator <<= exponent; else numerator <<= -exponent;
        while (numerator < denominator) { numerator <<= 1; exponent--; }
        while (numerator >= 2 * denominator) { denominator <<= 1; exponent++; }
        var mantissa = ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator);
        var result = new ExactMeanAccumulator(); result.Add(Math.Log(mantissa)); result.Add(Math.Log(2), exponent);
        return result.Mean(1);
    }
    internal double RawNext(double high, double low, double price, bool final)
    {
        var shortRange = _shortRange.Next(high, low, _index, final); var longRange = _longRange.Next(high, low, _index, final);
        var shortPrevious = _shortPrices.Count == _length ? _shortPrices.Peek() : 0;
        var longPrevious = _longPrices.Count == _longLength ? _longPrices.Peek() : 0;
        var small = U(Math.Max(shortPrevious, shortRange.High)) - U(Math.Min(shortPrevious, shortRange.Low));
        var big = U(Math.Max(longPrevious, longRange.High)) - U(Math.Min(longPrevious, longRange.Low));
        var sum = _sum + small - (_ranges.Count == _aggregation ? _ranges.Peek() : BigInteger.Zero);
        double dimension;
        if (_scale == 1) dimension = 0;
        else if (sum.IsZero || big.IsZero) dimension = 2;
        else
        {
            var log = LogRatio(big * _aggregation, sum);
            var numerator = new ExactMeanAccumulator(); numerator.Add(_logScale, 2); numerator.Add(log, -1);
            var denominator = new ExactMeanAccumulator(); denominator.Add(_logScale);
            dimension = numerator.Ratio(denominator);
        }
        if (final)
        {
            Push(_shortPrices, price, _length); Push(_longPrices, price, _longLength); Push(_ranges, small, _aggregation);
            _sum = sum; _index++;
        }
        return dimension;
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double price, bool final,
        double? externalLine = null, double? externalSignal = null)
    {
        var raw = RawNext(high, low, price, final);
        var line = externalLine ?? _line!.Next(raw, final); var signal = externalSignal ?? _signal!.Next(line, final);
        var spread = U(line) - U(signal);
        var trade = spread.Sign < 0 && spread < _previousSpread ? Signal.StrongBuy : spread.Sign > 0 && spread > _previousSpread ? Signal.StrongSell
            : spread.Sign < 0 ? Signal.Buy : spread.Sign > 0 ? Signal.Sell : Signal.None;
        if (final) _previousSpread = spread;
        return (line, signal, trade);
    }
    internal static (double[] Line, double[] Signal) Components(StockData data, List<double> input, List<double> high, List<double> low,
        MovingAvgType kind, int length, int scale, int smoothing, bool callbacks, bool includeSignal)
    {
        var caller = data.CaptureInputSeries(); smoothing = Math.Max(1, smoothing);
        using var window = new JrcWindow(kind, length, scale, smoothing, true);
        var raw = new double[input.Count]; for (var i = 0; i < input.Count; i++) raw[i] = window.RawNext(high[i], low[i], input[i], true);
        double[] Average(double[] values) => (callbacks ? ComponentAverage.Take(values, smoothing)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, smoothing, values.ToList()).ToArray();
        var line = Average(raw); var signal = includeSignal ? Average(line) : Array.Empty<double>();
        data.RestoreInputSeries(caller); return (line, signal);
    }
    private static void Push<T>(Queue<T> queue, T value, int length) { if (queue.Count == length) queue.Dequeue(); queue.Enqueue(value); }
    internal void Reset()
    {
        _shortRange.Reset(); _longRange.Reset(); _shortPrices.Clear(); _longPrices.Clear(); _ranges.Clear();
        _line?.Reset(); _signal?.Reset(); _sum = _previousSpread = default; _index = 0;
    }
    public void Dispose() { _line?.Dispose(); _signal?.Dispose(); }
    private sealed class Extrema
    {
        private readonly int _length;
        private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
        internal Extrema(int length) => _length = length;
        internal (double High, double Low) Next(double high, double low, long index, bool final) =>
            (Next(_highs, high, index, true, final), Next(_lows, low, index, false, final));
        private double Next(LinkedList<(long Index, double Value)> deque, double value, long index, bool maximum, bool final)
        {
            var expiry = index - _length + 1; var first = deque.First;
            while (first is not null && first.Value.Index < expiry) first = first.Next;
            var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
            if (final)
            {
                while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
                while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
                deque.AddLast((index, value));
            }
            return result;
        }
        internal void Reset() { _highs.Clear(); _lows.Clear(); }
    }
    private sealed class Mean : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<double> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Mean(MovingAvgType kind, int length)
        {
            _kind = kind; _length = Math.Max(1, length);
            if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, _length, 1);
            else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
        }
        internal double Next(double value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(new(value), final).Publish();
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); weighted.Add(value, _length);
            if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                : _history.Count < _length - 1 ? 0 : sum.Mean(_length);
            if (final) { _sum = sum; _weighted = weighted; Push(_history, value, _length); }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
