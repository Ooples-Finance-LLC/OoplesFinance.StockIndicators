using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendAnalysisIndicatorWindow : IDisposable
{
    private readonly int _width;
    private readonly Average _slow, _fast, _signal;
    private readonly Queue<Number> _history = new();
    private Number _sum, _squares, _previousSlope;
    internal TrendAnalysisIndicatorWindow(MovingAvgType kind, int length1, int length2)
    { _width = Math.Max(1, length2); _slow = new(kind, length1); _fast = new(kind, length2); _signal = new(kind, length1); }
    private Number Deviation(Number slow, bool final)
    {
        var full = _history.Count == _width; var count = full ? _width : _history.Count + 1;
        var sum = _sum + slow; var squares = _squares + slow * slow;
        if (full) { var expired = _history.Peek(); sum -= expired; squares -= expired * expired; }
        // The population variance is exact before the public deviation component is rounded.
        var variance = count < _width ? default : (squares - (sum * sum).Divide(count)).Divide(count);
        var deviation = ResidualVolatilityWindow.Root(variance);
        if (final) { if (full) _history.Dequeue(); _history.Enqueue(slow); _sum = sum; _squares = squares; }
        return deviation;
    }
    private (double Line, double SignalLine, Signal Trade) Finish(Number deviation, Number signal, Number slope, bool final)
    {
        var acceleration = slope - _previousSlope;
        var trade = (deviation - signal).Sign < 0 ? Signal.None
            : slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousSlope = slope;
        return (deviation.Publish(), signal.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        var slow = _slow.Next(value, final); var fast = _fast.Next(value, final); var deviation = Deviation(slow, final);
        return Finish(deviation, _signal.Next(deviation, final), fast - slow, final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length1, int length2, bool includeSignal = true, bool includeTrades = true)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var source = prices.Select(Number.Of).ToArray();
        Number[] Mean(Number[] values, int length)
        {
            // Record each requested component even during callback discovery.
            var custom = ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length);
            if (custom is not null) return Enumerable.Range(0, values.Length).Select(i => i < custom.Count ? Number.Of(custom[i]) : default).ToArray();
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        var slow = Mean(source, length1); var fast = includeTrades ? Mean(source, length2) : new Number[source.Length];
        using var window = new TrendAnalysisIndicatorWindow(kind, length1, length2);
        var deviation = slow.Select(v => window.Deviation(v, true)).ToArray();
        var signal = includeSignal ? Mean(deviation, length1) : new Number[source.Length]; var trades = new Signal[source.Length];
        if (includeTrades) for (var i = 0; i < source.Length; i++) trades[i] = window.Finish(deviation[i], signal[i], fast[i] - slow[i], true).Trade;
        return (deviation.Select(v => v.Publish()).ToArray(), signal.Select(v => v.Publish()).ToArray(), trades);
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
    internal void Reset() { _slow.Reset(); _fast.Reset(); _signal.Reset(); _history.Clear(); _sum = _squares = _previousSlope = default; }
    public void Dispose() { Reset(); _slow.Dispose(); _fast.Dispose(); _signal.Dispose(); }
}
