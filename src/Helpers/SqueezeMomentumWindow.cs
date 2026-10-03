using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SqueezeMomentumWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _average;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private readonly Queue<Number> _residuals = new();
    private Number _sum, _weighted, _previous;
    private long _count;
    internal SqueezeMomentumWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _average = new(kind, _length); }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _length + 1L;
        var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    private (double Value, Signal Trade) FromMean(double price, double high, double low, Number mean, bool final)
    {
        var highest = Extreme(_highs, high, true, final); var lowest = Extreme(_lows, low, false, final);
        var residual = Number.Of(price) - (Number.Of(highest) + Number.Of(lowest) + mean.Times(2)).Divide(4);
        var full = _residuals.Count == _length; var expired = full ? _residuals.Peek() : default;
        var n = full ? _residuals.Count : _residuals.Count + 1L;
        var sum = _sum + residual - expired;
        var weighted = full ? _weighted - _sum + expired + residual.Times(n - 1) : _weighted + residual.Times(n - 1);
        var value = (weighted.Times(6) + sum.Times(4 - 2 * n)).Divide(n * (n + 1));
        var change = value - _previous;
        var trade = value.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : value.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        { if (full) _residuals.Dequeue(); _residuals.Enqueue(residual); _sum = sum; _weighted = weighted; _previous = value; _count++; }
        return (value.Publish(), trade);
    }
    internal (double Value, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        return FromMean(price, high, low, _average.Next(Number.Of(price), final), final);
    }
    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var (prices, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var highs = data.HighPrices; var lows = data.LowPrices;
        for (var i = 0; i < prices.Count; i++)
        { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(highs[i], nameof(highs)); StreamingInputValidation.Finite(lows[i], nameof(lows)); }
        using var window = new SqueezeMomentumWindow(kind, length); var output = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        { for (var i = 0; i < prices.Count; i++) (output[i], trades[i]) = window.Next(prices[i], highs[i], lows[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                var mean = ComponentAverage.Take(prices.ToArray(), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, prices).ToArray();
                for (var i = 0; i < prices.Count; i++)
                { StreamingInputValidation.Finite(mean[i], nameof(mean)); (output[i], trades[i]) = window.FromMean(prices[i], highs[i], lows[i], Number.Of(mean[i]), true); }
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (output, trades);
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length); }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    internal void Reset() { _average.Reset(); _highs.Clear(); _lows.Clear(); _residuals.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    public void Dispose() { Reset(); _average.Dispose(); }
}
