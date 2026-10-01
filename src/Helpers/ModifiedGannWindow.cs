using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ModifiedGannWindow : IDisposable
{
    private readonly int _length;
    private readonly Number _mult;
    private readonly Queue<(double High, double Low)> _history = new();
    private readonly MacZWindow.Average _upper, _lower;
    private int _direction;
    private Number _previousMargin;
    internal ModifiedGannWindow(MovingAvgType kind, int length, double mult)
    {
        StreamingInputValidation.Finite(mult, nameof(mult));
        _length = Math.Max(1, length); _mult = Number.Of(mult);
        _upper = new(kind, _length); _lower = new(kind, _length);
    }
    private (Number Upper, Number Lower) Extensions(double high, double low, double open, double close, bool final)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        StreamingInputValidation.Finite(open, nameof(open)); StreamingInputValidation.Finite(close, nameof(close));
        var highest = high; var lowest = low; var skip = _history.Count == _length;
        foreach (var candle in _history)
        {
            if (skip) { skip = false; continue; }
            highest = Math.Max(highest, candle.High); lowest = Math.Min(lowest, candle.Low);
        }
        var top = Number.Of(Math.Max(open, close)); var bottom = Number.Of(Math.Min(open, close));
        var upper = top + (Number.Of(highest) - top) * _mult;
        var lower = bottom - (bottom - Number.Of(lowest)) * _mult;
        if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((high, low)); }
        return (upper, lower);
    }
    private (double Value, Signal Trade) Finish(double close, Number upper, Number lower, bool final)
    {
        var price = Number.Of(close);
        var direction = (price - upper).Sign > 0 ? 1 : (price - lower).Sign > 0 ? 0 : _direction;
        var line = direction == 1 ? lower : upper;
        var margin = price - line; var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _direction = direction; _previousMargin = margin; }
        return (line.Publish(), trade);
    }
    internal (double Value, Signal Trade) Next(double high, double low, double open, double close, bool final)
    {
        var extensions = Extensions(high, low, open, close, final);
        return Finish(close, _upper.Next(extensions.Upper, final), _lower.Next(extensions.Lower, final), final);
    }
    internal static (List<double> Values, List<Signal> Trades) Calculate(StockData data, IReadOnlyList<double> close,
        IReadOnlyList<double> high, IReadOnlyList<double> low, IReadOnlyList<double> open, MovingAvgType kind, int length, double mult, bool callbacks)
    {
        if (high.Count < close.Count || low.Count < close.Count || open.Count < close.Count) throw new ArgumentException("Candle fields must be at least input length.");
        using var window = new ModifiedGannWindow(kind, length, mult);
        var upper = new Number[close.Count]; var lower = new Number[close.Count];
        for (var i = 0; i < close.Count; i++) (upper[i], lower[i]) = window.Extensions(high[i], low[i], open[i], close[i], true);
        var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] source)
        {
            var published = callbacks || !StrengthWindow.Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, window._length) : null;
            if (custom is not null) return Enumerable.Range(0, source.Length).Select(i => i < custom.Count ? Number.Of(custom[i]) : default).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, window._length, published!.ToList()).Select(Number.Of).ToArray();
            using var average = new MacZWindow.Average(kind, window._length);
            return source.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var upperMean = Mean(upper); var lowerMean = Mean(lower);
            var values = new List<double>(close.Count); var trades = new List<Signal>(close.Count);
            for (var i = 0; i < close.Count; i++) { var point = window.Finish(close[i], upperMean[i], lowerMean[i], true); values.Add(point.Value); trades.Add(point.Trade); }
            return (values, trades);
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _history.Clear(); _upper.Reset(); _lower.Reset(); _direction = 0; _previousMargin = default; }
    public void Dispose() { _upper.Dispose(); _lower.Dispose(); }
}
