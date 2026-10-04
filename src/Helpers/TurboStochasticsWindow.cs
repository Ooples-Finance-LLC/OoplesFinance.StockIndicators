using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TurboStochasticsWindow : IDisposable
{
    private readonly bool _slow;
    private readonly Extrema _high, _low;
    private readonly Average _first, _second;
    private readonly Fit _lineFit, _signalFit;
    private Number _previousLine, _previousDifference;
    internal TurboStochasticsWindow(MovingAvgType kind, int length1, int length2, int turbo, bool slow)
    {
        _slow = slow; _high = new(length1, true); _low = new(length1, false);
        _first = new(kind, length1); _second = new(kind, length1);
        var period = RegressionPeriod(length2, turbo); _lineFit = new(period); _signalFit = new(period);
    }
    internal static long RegressionPeriod(int length, int turbo)
    { var resolved = Math.Max(1, length); return Math.Max(1L, (long)resolved + Math.Max(-resolved, Math.Min(resolved, turbo))); }
    private Number Raw(double price, double high, double low, bool final)
    {
        var upper = _high.Next(high, final); var lower = _low.Next(low, final);
        if (upper == lower) return default; // NOSONAR: S1244 - Equal extrema define the exact singularity.
        if (upper > lower) { if (price <= lower) return default; if (price >= upper) return Number.Of(100); }
        else { if (price >= lower) return default; if (price <= upper) return Number.Of(100); }
        return (Number.Of(price) - Number.Of(lower)).Times(100).Divide(Number.Of(upper) - Number.Of(lower));
    }
    private (double Line, double SignalLine, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var difference = line - signal; var change = difference - _previousDifference;
        var trade = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : difference.Sign > 0 || (_previousLine - Number.Of(30)).Sign < 0 && (line - Number.Of(30)).Sign > 0 ? Signal.Buy
            : difference.Sign < 0 || (_previousLine - Number.Of(70)).Sign > 0 && (line - Number.Of(70)).Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousLine = line; _previousDifference = difference; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var raw = Raw(price, high, low, final); var first = _first.Next(raw, final);
        var line = _lineFit.Next(_slow ? first : raw, final); var signal = _signalFit.Next(_slow ? _second.Next(first, final) : first, final);
        return Finish(line, signal, final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length1, int length2, int turbo, bool slow, bool batch = true, bool signalOutput = false)
    {
        length1 = Math.Max(1, length1); using var window = new TurboStochasticsWindow(kind, length1, length2, turbo, slow);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var raw = new Number[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(data.HighPrices[i], nameof(data.HighPrices)); StreamingInputValidation.Finite(data.LowPrices[i], nameof(data.LowPrices)); }
        for (var i = 0; i < prices.Count; i++) raw[i] = window.Raw(prices[i], data.HighPrices[i], data.LowPrices[i], true);
        Number[] Mean(Number[] values, int period)
        {
            var custom = ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), period);
            if (custom is not null) return Enumerable.Range(0, values.Length).Select(i => i < custom.Count ? Number.Of(custom[i]) : default).ToArray();
            using var average = new Average(kind, period); return values.Select(v => average.Next(v, true)).ToArray();
        }
        // Preserve the nested stochastic's two non-SMA component requests, even though only raw K is used.
        if (batch && kind != MovingAvgType.SimpleMovingAverage) { var unused = Mean(raw, 3); Mean(unused, 3); }
        var first = slow || batch || signalOutput ? Mean(raw, length1) : raw;
        var signalInput = slow && (batch || signalOutput) ? Mean(first, length1) : first;
        var line = new double[raw.Length]; var signal = new double[raw.Length]; var trades = new Signal[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var k = window._lineFit.Next(slow ? first[i] : raw[i], true); var d = window._signalFit.Next(signalInput[i], true);
            var point = window.Finish(k, d, true); line[i] = point.Line; signal[i] = point.SignalLine; trades[i] = point.Trade;
        }
        return (line, signal, trades);
    }
    private sealed class Extrema
    {
        private readonly int _length; private readonly bool _maximum; private long _index;
        private readonly LinkedList<(long Index, double Value)> _deque = new();
        internal Extrema(int length, bool maximum) { _length = Math.Max(1, length); _maximum = maximum; }
        internal double Next(double value, bool final)
        {
            var expiry = _index - _length + 1; var first = _deque.First;
            while (first is not null && first.Value.Index < expiry) first = first.Next;
            var result = first is null || (_maximum ? value > first.Value.Value : value < first.Value.Value) ? value : first.Value.Value;
            if (final)
            {
                while (_deque.First is { } old && old.Value.Index < expiry) _deque.RemoveFirst();
                while (_deque.Last is { } last && (_maximum ? last.Value.Value <= value : last.Value.Value >= value)) _deque.RemoveLast();
                _deque.AddLast((_index, value)); _index++;
            }
            return result;
        }
        internal void Reset() { _deque.Clear(); _index = 0; }
    }
    private sealed class Fit
    {
        private readonly long _length; private readonly Queue<Number> _values = new(); private Number _sum, _weighted;
        internal Fit(long length) => _length = length;
        internal Number Next(Number value, bool final)
        {
            var full = _values.Count == _length; var count = full ? _values.Count : _values.Count + 1;
            var old = full ? _values.Peek() : default; var sum = _sum + value - old;
            var weighted = full ? _weighted - _sum + old + value.Times(count - 1L) : _weighted + value.Times(count - 1L);
            var covariance = weighted.Times(2) - sum.Times(count - 1L);
            var result = sum.Divide(count) + covariance.Times(3).Divide(count).Divide(count + 1L);
            if (final) { if (full) _values.Dequeue(); _values.Enqueue(value); _sum = sum; _weighted = weighted; }
            return result;
        }
        internal void Reset() { _values.Clear(); _sum = _weighted = default; }
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _first.Reset(); _second.Reset(); _lineFit.Reset(); _signalFit.Reset(); _previousLine = _previousDifference = default; }
    public void Dispose() { Reset(); _first.Dispose(); _second.Dispose(); }
}
