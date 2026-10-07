using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Volume/maxVolume is a gain, not a separately rounded public output. Retain it
// and the recurrence exactly so tiny gains and recovery from overflowing values survive.
internal sealed class SvamaWindow
{
    private bool _hasPrevious;
    private double _highestVolume;
    private Number _previous, _previousComparison;
    internal (double Line, Signal Trade) Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var highest = _hasPrevious ? Math.Max(_highestVolume, volume) : volume;
        var current = Number.Of(price); var previous = _hasPrevious ? _previous : current;
        var line = highest == 0 ? previous : previous + (current - previous) * Number.Of(volume).Divide(Number.Of(highest));
        var comparison = current - line; var change = comparison - _previousComparison;
        var trade = comparison.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy
            : comparison.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _hasPrevious = true; _highestVolume = highest; _previous = line; _previousComparison = comparison; }
        return (line.Publish(), trade);
    }
    internal void Reset() { _hasPrevious = false; _highestVolume = 0; _previous = _previousComparison = default; }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var line = new double[prices.Count]; var trades = new Signal[prices.Count]; var window = new SvamaWindow();
        for (var i = 0; i < prices.Count; i++) (line[i], trades[i]) = window.Next(prices[i], data.Volumes[i], true);
        return (line, trades);
    }
}
