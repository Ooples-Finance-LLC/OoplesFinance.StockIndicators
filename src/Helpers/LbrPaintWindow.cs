using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class LbrPaintWindow : IDisposable
{
    private readonly int _lookback;
    private readonly double _multiplier;
    private readonly Average _mean;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index;
    private double _previousPrice;
    private ExactMeanAccumulator _previousBullish, _previousBearish;
    internal LbrPaintWindow(MovingAvgType kind, int length, int lookback, double multiplier)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        _lookback = Math.Max(1, lookback); _multiplier = multiplier; _mean = new(kind, Math.Max(1, length));
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _lookback + 1L; var first = deque.First;
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
    internal (double Upper, double Lower, double Width, Signal Trade) Next(double high, double low, double price, bool final, double? externalAtr = null)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(price, nameof(price));
        var range = FearGreedWindow.TrueRange(high, low, _index == 0 ? price : _previousPrice);
        var atr = externalAtr.HasValue ? new RocBankValue(externalAtr.Value) : _mean.Next(range, final);
        var width = new ExactMeanAccumulator(); width.AddProduct(atr.Mantissa, _multiplier); width.ScaleByPowerOfTwo(atr.UpperShift);
        var upper = new ExactMeanAccumulator(); upper.Add(Extreme(_highs, high, true, final)); upper.Subtract(width);
        var lower = width; lower.Add(Extreme(_lows, low, false, final));
        var order = upper; order.Subtract(lower);
        var bullish = new ExactMeanAccumulator(); bullish.Add(price); bullish.Subtract(order.Sign >= 0 ? upper : lower);
        var bearish = new ExactMeanAccumulator(); bearish.Add(price); bearish.Subtract(order.Sign >= 0 ? lower : upper);
        var bullChange = bullish; bullChange.Subtract(_previousBullish); var bearChange = bearish; bearChange.Subtract(_previousBearish);
        var trade = bullish.Sign > 0 && bullChange.Sign > 0 ? Signal.StrongBuy : bearish.Sign < 0 && bearChange.Sign < 0 ? Signal.StrongSell
            : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousPrice = price; _index++; _previousBullish = bullish; _previousBearish = bearish; }
        return (upper.Mean(1), lower.Mean(1), width.Mean(1), trade);
    }
    internal static (double[] Upper, double[] Lower, double[] Width, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, int lookback, double multiplier, bool callbacks)
    {
        using var window = new LbrPaintWindow(kind, length, lookback, multiplier);
        var (prices, highs, lows, _, _) = CalculationsHelper.GetInputValuesList(data);
        var caller = data.CaptureInputSeries();
        try
        {
            IReadOnlyList<double>? external = null;
            if (!StrengthWindow.Supports(kind) || (callbacks && ComponentAverage.HasOverrides))
            {
                var ranges = Enumerable.Range(0, prices.Count).Select(i => FearGreedWindow.TrueRange(highs[i], lows[i], i == 0 ? prices[i] : prices[i - 1]).Publish()).ToArray();
                if (callbacks) external = ComponentAverage.Take(ranges, Math.Max(1, length));
                if (external is null && !StrengthWindow.Supports(kind)) external = CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, length), ranges.ToList());
            }
            var upper = new double[prices.Count]; var lower = new double[prices.Count]; var width = new double[prices.Count]; var trades = new Signal[prices.Count];
            for (var i = 0; i < prices.Count; i++) (upper[i], lower[i], width[i], trades[i]) = window.Next(highs[i], lows[i], prices[i], true, external?[i]);
            return (upper, lower, width, trades);
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _mean.Reset(); _index = 0; _previousPrice = 0; _previousBullish = _previousBearish = default; }
    public void Dispose() => _mean.Dispose();
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
