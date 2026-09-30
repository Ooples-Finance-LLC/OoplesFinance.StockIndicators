using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FireflyWindow : IDisposable
{
    private readonly int _length, _smooth;
    private readonly Average? _center, _first, _second, _last;
    private readonly Queue<double> _prices = new();
    private readonly LinkedList<(long Index, RocBankValue Value)> _maximum = new();
    private BigInteger _sum, _squares, _previousLine, _previousSlope; private long _index;
    internal static bool Supports(MovingAvgType kind) => StrengthWindow.Supports(kind) || kind == MovingAvgType.ZeroLagExponentialMovingAverage;
    internal FireflyWindow(MovingAvgType kind, int length, int smooth, bool means = true)
    {
        _length = Math.Max(1, length); _smooth = Math.Max(1, smooth);
        if (means) { _center = new(kind, _length); _first = new(kind, _smooth); _second = new(kind, _smooth); _last = new(kind, _length); }
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal static double Weighted(double high, double low, double price)
    { var sum = new ExactMeanAccumulator(); sum.Add(high); sum.Add(low); sum.Add(price, 2); return sum.Mean(4); }
    internal RocBankValue Normalize(double price, RocBankValue mean, bool final)
    {
        var current = ExactVarianceWindow.Units(price); var sum = _sum + current; var squares = _squares + current * current;
        if (_prices.Count == _length) { var old = ExactVarianceWindow.Units(_prices.Peek()); sum -= old; squares -= old * old; }
        var radicand = _prices.Count < _length - 1 ? BigInteger.Zero : _length * squares - sum * sum;
        var difference = new ExactMeanAccumulator(); difference.Add(price, 100); mean.AddTo(ref difference, -100);
        RocBankValue normalized;
        if (radicand.IsZero) normalized = RocBankValue.Round(difference);
        else
        {
            var numerator = (current - Units(mean)) * 100 * _length; var numeratorSquared = (numerator * numerator) << 2148;
            var shift = 0; var magnitude = ExactPopulationDeviation.RootRatio(numeratorSquared, radicand);
            while (double.IsInfinity(magnitude)) { shift += 1024; magnitude = ExactPopulationDeviation.RootRatio(numeratorSquared, radicand << (2 * shift)); }
            normalized = new(numerator.Sign < 0 ? -magnitude : magnitude, shift);
        }
        if (final) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); _sum = sum; _squares = squares; }
        return normalized;
    }
    internal (double Line, double SignalLine, Signal Trade) Finish(RocBankValue filtered, bool final)
    {
        var shifted = new ExactMeanAccumulator(); filtered.AddTo(ref shifted); shifted.Add(92); var line = RocBankValue.Round(shifted, count: 2);
        var current = Units(line); var slope = current - _previousLine; var expiry = _index - _smooth + 1L; var first = _maximum.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var maximum = first is not null && Units(first.Value.Value) > current ? first.Value.Value : line;
        var trade = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        // RSI threshold crossings imply the same slope direction, so they add no branch here.
        if (final)
        {
            while (_maximum.First is { } old && old.Value.Index < expiry) _maximum.RemoveFirst();
            while (_maximum.Last is { } tail && Units(tail.Value.Value) <= current) _maximum.RemoveLast();
            _maximum.AddLast((_index, line)); _index++; _previousLine = current; _previousSlope = slope;
        }
        return (line.Publish(), maximum.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double price, bool final)
    {
        var weighted = Weighted(high, low, price); var center = _center!.Next(new(weighted), final);
        var standardized = Normalize(weighted, center, final);
        var first = _first!.Next(standardized, final); var second = _second!.Next(first, final); var last = _last!.Next(second, final);
        return Finish(last, final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, List<double> input, List<double> highs, List<double> lows,
        MovingAvgType kind, int length, int smooth, bool callbacks)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth); var caller = data.CaptureInputSeries();
        RocBankValue[] Mean(RocBankValue[] source, int period)
        {
            var published = callbacks || !Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, period) : null;
            if (custom is not null) return custom.Select(v => new RocBankValue(v)).ToArray();
            if (!Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, period, published!.ToList()).Select(v => new RocBankValue(v)).ToArray();
            using var mean = new Average(kind, period); return source.Select(v => mean.Next(v, true)).ToArray();
        }
        try
        {
            var weighted = Enumerable.Range(0, input.Count).Select(i => Weighted(highs[i], lows[i], input[i])).ToArray();
            var center = Mean(weighted.Select(v => new RocBankValue(v)).ToArray(), length);
            using var window = new FireflyWindow(kind, length, smooth, false);
            var standardized = Enumerable.Range(0, input.Count).Select(i => window.Normalize(weighted[i], center[i], true)).ToArray();
            var first = Mean(standardized, smooth); var second = Mean(first, smooth); var last = Mean(second, length);
            var result = last.Select(v => window.Finish(v, true)).ToArray();
            return (result.Select(v => v.Line).ToArray(), result.Select(v => v.SignalLine).ToArray(), result.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _center?.Reset(); _first?.Reset(); _second?.Reset(); _last?.Reset(); _prices.Clear(); _maximum.Clear(); _sum = _squares = _previousLine = _previousSlope = default; _index = 0; }
    public void Dispose() { _center?.Dispose(); _first?.Dispose(); _second?.Dispose(); _last?.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive, _second; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind == MovingAvgType.ZeroLagExponentialMovingAverage) { _recursive = new(MovingAvgType.ExponentialMovingAverage, length, 1); _second = new(MovingAvgType.ExponentialMovingAverage, length, 1); } else if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null)
            {
                var first = _recursive.Next(value, commit); if (_second is null) return first;
                var second = _second.Next(first, commit); var extrapolated = new ExactMeanAccumulator(); first.AddTo(ref extrapolated, 2); second.AddTo(ref extrapolated, -1); return RocBankValue.Round(extrapolated);
            }
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _second?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _second?.Dispose(); _fallback?.Dispose(); }
    }
}
