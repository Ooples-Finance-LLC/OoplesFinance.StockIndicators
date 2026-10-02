using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// The range and numerator may exceed binary64 even though their ratio is in [0, 1].
// Keep the ratio exact through the two signal differences as well.
internal sealed class SupportResistanceOscillatorWindow
{
    private double _previousClose;
    private bool _hasPrevious;
    private Number _previous, _previousSlope;
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    internal (double Line, Signal Trade) Next(double open, double high, double low, double close, bool final)
    {
        StreamingInputValidation.Finite(open, nameof(open)); StreamingInputValidation.Finite(high, nameof(high));
        StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(close, nameof(close));
        var h = Number.Of(high); var l = Number.Of(low); var c = Number.Of(close);
        var previousClose = Number.Of(_hasPrevious ? _previousClose : close);
        var range = h - l;
        var highGap = Abs(h - previousClose); var lowGap = Abs(l - previousClose);
        if ((highGap - range).Sign > 0) range = highGap;
        if ((lowGap - range).Sign > 0) range = lowGap;
        var line = range.Sign == 0 ? default : (h - Number.Of(open) + (c - l)).Divide(range.Times(2));
        if (line.Sign < 0) line = default;
        if ((line - Number.Of(1)).Sign > 0) line = Number.Of(1);
        var slope = line - _previous; var acceleration = slope - _previousSlope;
        // Threshold crossings imply the same slope direction; the strong-signal branches have priority.
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
            : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousClose = close; _hasPrevious = true; _previous = line; _previousSlope = slope; }
        return (line.Publish(), trade);
    }
    internal void Reset() { _previousClose = 0; _hasPrevious = false; _previous = _previousSlope = default; }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var line = new double[prices.Count]; var trades = new Signal[prices.Count]; var window = new SupportResistanceOscillatorWindow();
        for (var i = 0; i < prices.Count; i++)
        {
            StreamingInputValidation.Finite(data.Volumes[i], nameof(data));
            (line[i], trades[i]) = window.Next(data.OpenPrices[i], data.HighPrices[i], data.LowPrices[i], prices[i], true);
        }
        return (line, trades);
    }
}
