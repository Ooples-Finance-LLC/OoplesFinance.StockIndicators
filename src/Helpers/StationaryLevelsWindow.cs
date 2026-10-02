using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class StationaryLevelsWindow : IDisposable
{
    private readonly int _length;
    private readonly long _width;
    private readonly Average _mean;
    private readonly Queue<Number> _recent = new(), _older = new();
    private readonly LinkedList<(long Index, Number Value)> _highs = new(), _lows = new();
    private long _index;
    private Number _previous;
    internal StationaryLevelsWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _width = (long)_length + Math.Max(2, _length) - 1; _mean = new(kind, _length); }
    private Number Extreme(LinkedList<(long Index, Number Value)> deque, Number value, bool maximum, bool final)
    {
        var expiry = _index - _width + 1;
        var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null || (maximum ? (value - first.Value.Value).Sign > 0 : (value - first.Value.Value).Sign < 0) ? value : first.Value.Value;
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? (last.Value.Value - value).Sign <= 0 : (last.Value.Value - value).Sign >= 0)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    private (double Upper, double Middle, double Lower, double Deviation, Signal Trade) FromMean(Number price, Number mean, bool final)
    {
        var residual = price - mean;
        var recent = _recent.Count == _length ? _recent.Peek() : default;
        var older = _older.Count == _length ? _older.Peek() : default;
        var ext = _index <= _length || (recent - older).Sign == 0 ? default
            : _index < 2L * _length ? recent.Times(_index).Divide(2 * (_index - _length)) : recent - older.Divide(2);
        var upper = Extreme(_highs, ext, true, final); var lower = Extreme(_lows, ext, false, final);
        var midpoint = (upper + lower).Divide(2); var comparison = residual - ext; var change = comparison - _previous;
        var trade = comparison.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : comparison.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_recent.Count == _length)
            { var expired = _recent.Dequeue(); if (_older.Count == _length) _older.Dequeue(); _older.Enqueue(expired); }
            _recent.Enqueue(residual); _previous = comparison; _index++;
        }
        return (upper.Publish(), midpoint.Publish(), lower.Publish(), residual.Publish(), trade);
    }
    internal (double Upper, double Middle, double Lower, double Deviation, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        return FromMean(value, _mean.Next(value, final), final);
    }
    internal static (double[] Upper, double[] Middle, double[] Lower, double[] Deviation, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new StationaryLevelsWindow(kind, length); var upper = new double[prices.Count]; var middle = new double[prices.Count]; var lower = new double[prices.Count]; var deviation = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        {
            for (var i = 0; i < prices.Count; i++) (upper[i], middle[i], lower[i], deviation[i], trades[i]) = window.Next(prices[i], true);
        }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                var mean = ComponentAverage.Take(prices.ToArray(), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, prices).ToArray();
                for (var i = 0; i < prices.Count; i++)
                { StreamingInputValidation.Finite(mean[i], nameof(mean)); (upper[i], middle[i], lower[i], deviation[i], trades[i]) = window.FromMean(Number.Of(prices[i]), Number.Of(mean[i]), true); }
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (upper, middle, lower, deviation, trades);
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
    internal void Reset() { _mean.Reset(); _recent.Clear(); _older.Clear(); _highs.Clear(); _lows.Clear(); _index = 0; _previous = default; }
    public void Dispose() { Reset(); _mean.Dispose(); }
}
