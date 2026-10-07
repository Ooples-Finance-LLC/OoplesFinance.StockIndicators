using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
using Average = OoplesFinance.StockIndicators.Helpers.MacZWindow.Average;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VariableAdaptiveWindow : IDisposable
{
    private readonly Average _close, _open, _high, _low;
    private Number _previous, _previousPrice;
    private bool _hasPrevious;
    internal VariableAdaptiveWindow(MovingAvgType kind, int length)
    { _close = new(kind, length); _open = new(kind, length); _high = new(kind, length); _low = new(kind, length); }

    private (double Value, Signal Trade) Finish(Number price, Number close, Number open, Number high, Number low, bool final)
    {
        var width = high - low; var body = close - open;
        if (body.Sign < 0) body = body.Times(-1);
        Number gain = default;
        if (width.Sign != 0)
        {
            gain = body.Divide(width); var minimum = Number.Of(.01); var maximum = Number.Of(.99);
            if ((gain - minimum).Sign < 0) gain = minimum;
            if ((gain - maximum).Sign > 0) gain = maximum;
        }
        var previous = _hasPrevious ? _previous : price;
        var value = previous + gain * (price - previous);
        var margin = price - value; var oldMargin = _previousPrice - previous; var change = margin - oldMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previous = value; _previousPrice = price; _hasPrevious = true; }
        return (value.Publish(), trade);
    }
    internal (double Value, Signal Trade) Next(OhlcvBar bar, bool final)
    {
        StreamingInputValidation.Validate(bar);
        var price = Number.Of(bar.Close);
        return Finish(price, _close.Next(price, final), _open.Next(Number.Of(bar.Open), final),
            _high.Next(Number.Of(bar.High), final), _low.Next(Number.Of(bar.Low), final), final);
    }
    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        var (prices, highs, lows, opens, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        Number[] Mean(List<double> values)
        {
            var replacement = ComponentAverage.Take(values.ToArray(), length);
            if (replacement is not null) return Enumerable.Range(0, prices.Count).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            using var average = new Average(kind, length);
            return values.Select(v => average.Next(Number.Of(v), true)).ToArray();
        }
        // Preserve the four component callback slots, including selected range projection.
        var c = Mean(prices); var o = Mean(opens); var h = Mean(highs); var l = Mean(lows);
        using var window = new VariableAdaptiveWindow(kind, length);
        var values = new double[prices.Count]; var trades = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var point = window.Finish(Number.Of(prices[i]), c[i], o[i], h[i], l[i], true);
            values[i] = point.Value; trades[i] = point.Trade;
        }
        return (values, trades);
    }
    internal void Reset() { _close.Reset(); _open.Reset(); _high.Reset(); _low.Reset(); _previous = _previousPrice = default; _hasPrevious = false; }
    public void Dispose() { _close.Dispose(); _open.Dispose(); _high.Dispose(); _low.Dispose(); }
}
