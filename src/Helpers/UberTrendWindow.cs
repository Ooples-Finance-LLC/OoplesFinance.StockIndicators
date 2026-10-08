using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UberTrendWindow
{
    private readonly int _length;
    private readonly Queue<(Number Up, Number Down, Number UpVolume, Number DownVolume)> _history = new();
    private Number _up, _down, _upVolume, _downVolume, _previousPrice, _previousValue, _previousSlope;
    private bool _started;
    internal UberTrendWindow(int length) => _length = Math.Max(1, length);
    internal (double Value, Signal Trade) Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var current = Number.Of(price); var change = _started ? current - _previousPrice : default;
        var advance = change.Sign > 0 ? change : default; var decline = change.Sign < 0 ? default(Number) - change : default;
        var full = _history.Count == _length; var old = full ? _history.Peek() : default;
        var up = _up + advance - old.Up; var down = _down + decline - old.Down;
        var upContribution = change.Sign > 0 && up.Sign != 0 ? Number.Of(volume).Divide(up) : default;
        var downContribution = change.Sign < 0 && down.Sign != 0 ? Number.Of(volume).Divide(down) : default;
        var upVolume = _upVolume + upContribution - old.UpVolume; var downVolume = _downVolume + downContribution - old.DownVolume;
        var numerator = up * downVolume; var denominator = down * upVolume;
        // Preserve the nested quotient's defined zero-denominator cases before cross multiplication.
        var value = down.Sign == 0 || upVolume.Sign == 0 || downVolume.Sign == 0 ? Number.Of(-1)
            : (numerator + denominator).Sign == 0 ? default : (numerator - denominator).Divide(numerator + denominator);
        var slope = value - _previousValue; var acceleration = slope - _previousSlope;
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _history.Dequeue(); _history.Enqueue((advance, decline, upContribution, downContribution));
            _up = up; _down = down; _upVolume = upVolume; _downVolume = downVolume; _previousPrice = current; _previousValue = value; _previousSlope = slope; _started = true;
        }
        return (value.Publish(), trade);
    }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, int length)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues; var volume = data.Volumes;
        for (var i = 0; i < prices.Count; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(volume[i], nameof(volume)); }
        var window = new UberTrendWindow(length); var values = new double[prices.Count]; var signals = new Signal[prices.Count];
        for (var i = 0; i < values.Length; i++) { var point = window.Next(prices[i], volume[i], true); values[i] = point.Value; signals[i] = point.Trade; }
        return (values, signals);
    }
    internal void Reset() { _history.Clear(); _up = _down = _upVolume = _downVolume = _previousPrice = _previousValue = _previousSlope = default; _started = false; }
}
