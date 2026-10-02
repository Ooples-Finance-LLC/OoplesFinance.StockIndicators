using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RainbowWindow : IDisposable
{
    private readonly int _range;
    private readonly Average[] _stages;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count;
    private Number _previous;
    internal RainbowWindow(MovingAvgType kind, int length, int range)
    { _range = Math.Max(2, range); _stages = Enumerable.Range(0, 10).Select(_ => new Average(kind, length)).ToArray(); }
    private double Extreme(LinkedList<(long Index, double Value)> queue, double price, bool high, bool final)
    {
        var expiry = _count - _range + 1L; var first = queue.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? price : high ? Math.Max(price, first.Value.Value) : Math.Min(price, first.Value.Value);
        if (final)
        {
            while (queue.First is { } old && old.Value.Index < expiry) queue.RemoveFirst();
            while (queue.Last is { } last && (high ? last.Value.Value <= price : last.Value.Value >= price)) queue.RemoveLast();
            queue.AddLast((_count, price));
        }
        return result;
    }
    private (double Line, double Upper, double Lower, Signal Trade) Finish(double price, Number sum, Number low, Number high, bool final)
    {
        var width = Number.Of(Extreme(_highs, price, true, final)) - Number.Of(Extreme(_lows, price, false, final));
        var line = width.Sign == 0 ? default : (Number.Of(price).Times(10) - sum).Times(10).Divide(width);
        var band = width.Sign == 0 ? default : (high - low).Times(100).Divide(width);
        var change = line - _previous;
        var trade = line.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : line.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : line.Sign > 0 ? Signal.Buy : line.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previous = line; _count++; }
        var upper = band.Publish(); return (line.Publish(), upper, -upper, trade);
    }
    internal (double Line, double Upper, double Lower, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price); var sum = default(Number); var low = default(Number); var high = default(Number);
        for (var i = 0; i < _stages.Length; i++)
        {
            value = _stages[i].Next(value, final); sum += value;
            if (i == 0 || (value - low).Sign < 0) low = value;
            if (i == 0 || (value - high).Sign > 0) high = value;
        }
        return Finish(price, sum, low, high, final);
    }
    internal static (double[] Line, double[] Upper, double[] Lower, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, int range, bool callbacks)
    {
        length = Math.Max(1, length); var (prices, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new RainbowWindow(kind, length, range);
        var line = new double[prices.Count]; var upper = new double[prices.Count]; var lower = new double[prices.Count]; var trades = new Signal[prices.Count];
        if ((!callbacks || !ComponentAverage.HasOverrides) && StrengthWindow.Supports(kind))
        { for (var i = 0; i < prices.Count; i++) (line[i], upper[i], lower[i], trades[i]) = window.Next(prices[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                var values = prices.ToArray(); var sums = new Number[prices.Count]; var lows = new Number[prices.Count]; var highs = new Number[prices.Count];
                for (var stage = 0; stage < 10; stage++)
                {
                    values = (callbacks ? ComponentAverage.Take(values, length)?.ToArray() : null)
                        ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller);
                    for (var i = 0; i < values.Length; i++)
                    {
                        var value = Number.Of(values[i]); sums[i] += value;
                        if (stage == 0 || (value - lows[i]).Sign < 0) lows[i] = value;
                        if (stage == 0 || (value - highs[i]).Sign > 0) highs[i] = value;
                    }
                }
                for (var i = 0; i < prices.Count; i++) (line[i], upper[i], lower[i], trades[i]) = window.Finish(prices[i], sums[i], lows[i], highs[i], true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (line, upper, lower, trades);
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
    internal void Reset() { foreach (var stage in _stages) stage.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previous = default; }
    public void Dispose() { Reset(); foreach (var stage in _stages) stage.Dispose(); }
}
