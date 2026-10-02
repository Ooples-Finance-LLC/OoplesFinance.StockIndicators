using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PremierStochasticWindow : IDisposable
{
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private readonly Mean? _first, _second;
    private long _count;
    internal static int Resolve(int smooth) => Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, smooth)))));
    internal PremierStochasticWindow(MovingAvgType kind, int length, int smooth, bool external = false)
    {
        _length = Math.Max(1, length); var period = Resolve(smooth);
        if (!external) { _first = new(kind, period); _second = new(kind, period); }
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _length + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal double Raw(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var upper = Extreme(_highs, high, true, final); var lower = Extreme(_lows, low, false, final);
        double value;
        if (upper == lower) value = -5; // NOSONAR: S1244 - An exactly flat range defines zero stochastic.
        else if (upper > lower && price <= lower || upper < lower && price >= lower) value = -5;
        else if (upper > lower && price >= upper || upper < lower && price <= upper) value = 5;
        else
        {
            // Center before rounding, so a near-midpoint price never subtracts
            // two equal published stochastic values.
            var top = new ExactMeanAccumulator(); top.Add(price, 10); top.Add(lower, -5); top.Add(upper, -5);
            var bottom = new ExactMeanAccumulator(); bottom.Add(upper); bottom.Add(lower, -1);
            value = top.Ratio(bottom);
        }
        if (final) _count++;
        return value;
    }
    internal static double HalfTanh(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value); var magnitude = bits & long.MaxValue;
        // For subnormal x/2, odd input units are exact midpoints. Since
        // 0 < tanh(x) < x for x>0, choose the lower magnitude at these ties.
        if (magnitude < 0x0020000000000000L) return BitConverter.Int64BitsToDouble((bits & long.MinValue) | (magnitude / 2));
        return Math.Tanh(value / 2);
    }
    internal static Signal Trade(double current, double previous, double before)
    {
        var slope = ExactVarianceWindow.Units(current) - ExactVarianceWindow.Units(previous);
        var oldSlope = ExactVarianceWindow.Units(previous) - ExactVarianceWindow.Units(before);
        return slope.Sign > 0 && slope > oldSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < oldSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
    }
    internal double Next(double price, double high, double low, bool final)
        => HalfTanh(_second!.Next(_first!.Next(Raw(price, high, low, final), final), final));
    internal static double[] Calculate(StockData data, MovingAvgType kind, int length, int smooth, bool callbacks)
    {
        var (prices, highs, lows, _, _) = CalculationsHelper.GetInputValuesList(data); var caller = data.CaptureInputSeries();
        foreach (var p in prices) StreamingInputValidation.Finite(p, nameof(prices));
        foreach (var p in highs) StreamingInputValidation.Finite(p, nameof(highs));
        foreach (var p in lows) StreamingInputValidation.Finite(p, nameof(lows));
        try
        {
            var external = callbacks && ComponentAverage.HasOverrides || !StrengthWindow.Supports(kind);
            using var window = new PremierStochasticWindow(kind, length, smooth, external); var period = Resolve(smooth);
            var raw = Enumerable.Range(0, prices.Count).Select(i => window.Raw(prices[i], highs[i], lows[i], true)).ToArray();
            double[] Average(double[] values)
            {
                var result = (callbacks ? ComponentAverage.Take(values, period)?.ToArray() : null)
                    ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values.ToList()).ToArray();
                data.RestoreInputSeries(caller); return result;
            }
            var first = external ? Average(raw) : raw.Select(v => window._first!.Next(v, true)).ToArray();
            var second = external ? Average(first) : first.Select(v => window._second!.Next(v, true)).ToArray();
            return second.Select(HalfTanh).ToArray();
        }
        finally { data.RestoreInputSeries(caller); }
    }
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<double> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private ExactMeanAccumulator _sum, _weighted; private double _previous; private long _count;
        internal Mean(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal double Next(double value, bool final)
        {
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; double result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted.Subtract(sum); weighted.Add(value, _length);
                if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? 0 : sum.Mean(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum.Add(value); result = sum.Mean(_count + 1); }
            else
            {
                var next = new ExactMeanAccumulator(); next.Add(_previous, _length - 1);
                next.Add(value, _kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1);
                result = next.Mean(_kind == MovingAvgType.ExponentialMovingAverage ? _length + 1L : _length);
            }
            if (final)
            {
                if (finite) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _previous = 0; _count = 0; _fallback?.Reset(); }
        public void Dispose() { _fallback?.Dispose(); _history.Clear(); }
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _first?.Reset(); _second?.Reset(); _count = 0; }
    public void Dispose() { _highs.Clear(); _lows.Clear(); _first?.Dispose(); _second?.Dispose(); }
}
