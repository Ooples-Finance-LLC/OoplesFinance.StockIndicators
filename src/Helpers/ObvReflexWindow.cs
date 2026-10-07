using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ObvReflexWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly MacZWindow.Average _signal;
    private Number _total, _previousMargin;
    internal ObvReflexWindow(MovingAvgType kind, int length, int signalLength)
    { _length = Math.Max(1, length); _signal = new(kind, Math.Max(1, signalLength)); }
    private Number Total(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var previous = _prices.Count == _length ? _prices.Peek() : 0;
        var result = price > previous ? _total + Number.Of(volume) : price < previous ? _total - Number.Of(volume) : _total;
        if (final)
        {
            if (_prices.Count == _length) _prices.Dequeue();
            _prices.Enqueue(price); _total = result;
        }
        return result;
    }
    private (Number Line, Number SignalLine, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var margin = line - signal; var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousMargin = margin;
        return (line, signal, trade);
    }
    internal (Number Line, Number SignalLine, Signal Trade) Next(double price, double volume, bool final)
    {
        var line = Total(price, volume, final);
        return Finish(line, _signal.Next(line, final), final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, List<double> prices,
        MovingAvgType kind, int length, int signalLength, bool callbacks, bool includeSignal = true)
    {
        if (data.Volumes.Count < prices.Count) throw new ArgumentException("Volume input is shorter than selected prices.", nameof(data));
        for (var i = 0; i < prices.Count; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(data.Volumes[i], nameof(data.Volumes)); }
        var caller = data.CaptureInputSeries();
        try
        {
            using var window = new ObvReflexWindow(kind, length, signalLength);
            var line = prices.Select((value, i) => window.Total(value, data.Volumes[i], true)).ToArray();
            if (!includeSignal) return (line.Select(v => v.Publish()).ToArray(), Array.Empty<double>(), Array.Empty<Signal>());
            signalLength = Math.Max(1, signalLength);
            var published = callbacks || !StrengthWindow.Supports(kind) ? line.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, signalLength) : null;
            var signal = custom is not null ? custom.Select(Number.Of).ToArray()
                : !StrengthWindow.Supports(kind) ? CalculationsHelper.GetMovingAverageList(data, kind, signalLength, published!.ToList()).Select(Number.Of).ToArray()
                : line.Select(v => window._signal.Next(v, true)).ToArray();
            var points = line.Select((v, i) => window.Finish(v, signal[i], true)).ToArray();
            return (points.Select(v => v.Line.Publish()).ToArray(), points.Select(v => v.SignalLine.Publish()).ToArray(), points.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _prices.Clear(); _total = _previousMargin = default; _signal.Reset(); }
    public void Dispose() { _prices.Clear(); _signal.Dispose(); }
}
