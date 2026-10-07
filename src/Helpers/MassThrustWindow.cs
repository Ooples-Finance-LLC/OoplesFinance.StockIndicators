using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Retain the nested rolling ratios until the final public projection. A finite
// price difference can exceed binary64, and volume divided by a tiny move can
// overflow even when its subsequent product is small.
internal sealed class MassThrustWindow : IDisposable
{
    private readonly Sum _advances, _declines, _upVolumes, _downVolumes;
    private readonly MacZWindow.Average _signal;
    private readonly bool _oscillator;
    private Number _previousPrice, _previousLine, _previousSlope;
    private bool _hasPrevious;
    internal MassThrustWindow(bool oscillator, MovingAvgType kind, int length)
    {
        _oscillator = oscillator; length = Math.Max(1, length);
        _advances = new(length); _declines = new(length); _upVolumes = new(length); _downVolumes = new(length);
        _signal = new(kind, length);
    }
    private sealed class Sum
    {
        private readonly int _length; private readonly Queue<Number> _history = new(); private Number _sum;
        internal Sum(int length) => _length = length;
        internal Number Next(Number value, bool final)
        {
            var sum = _sum + value; if (_history.Count == _length) sum -= _history.Peek();
            if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); _sum = sum; }
            return sum;
        }
        internal void Reset() { _history.Clear(); _sum = default; }
    }
    internal (double Value, double SignalLine, Signal Trade) Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var current = Number.Of(price); var change = _hasPrevious ? current - _previousPrice : default;
        var advances = _advances.Next(change.Sign > 0 ? change : default, final);
        var declines = _declines.Next(change.Sign < 0 ? default(Number) - change : default, final);
        var up = _upVolumes.Next(change.Sign > 0 ? Number.Of(volume).Divide(advances) : default, final);
        var down = _downVolumes.Next(change.Sign < 0 ? Number.Of(volume).Divide(declines) : default, final);
        var positive = advances * up; var negative = declines * down;
        var denominator = positive + negative;
        var line = _oscillator ? denominator.Sign == 0 ? default : (positive - negative).Times(100).Divide(denominator)
            : (positive - negative).Divide(1000000);
        var signal = _signal.Next(line, final);
        var slope = _oscillator ? line - signal : signal; var acceleration = slope - _previousSlope;
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 || _oscillator && (_previousLine - Number.Of(-50)).Sign < 0 && (line - Number.Of(-50)).Sign > 0 ? Signal.Buy
            : slope.Sign < 0 || _oscillator && (_previousLine - Number.Of(50)).Sign > 0 && (line - Number.Of(50)).Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousPrice = current; _previousLine = line; _previousSlope = slope; _hasPrevious = true; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal static (List<double> Values, List<double> SignalLine, List<Signal> Trades) Calculate(IReadOnlyList<double> prices, IReadOnlyList<double> volumes, bool oscillator, MovingAvgType kind, int length)
    {
        if (volumes.Count < prices.Count) throw new ArgumentException("Volume must be at least input length.", nameof(volumes));
        for (var i = 0; i < prices.Count; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(volumes[i], nameof(volumes)); }
        using var window = new MassThrustWindow(oscillator, kind, length);
        var values = new List<double>(prices.Count); var signals = new List<double>(prices.Count); var trades = new List<Signal>(prices.Count);
        for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], volumes[i], true); values.Add(point.Value); signals.Add(point.SignalLine); trades.Add(point.Trade); }
        return (values, signals, trades);
    }
    internal static void Compute(ReadOnlySpan<double> prices, ReadOnlySpan<double> volumes, Span<double> output, int length, bool oscillator, bool unitVolume = false)
    {
        if (output.Length < prices.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (!unitVolume && volumes.Length < prices.Length) throw new ArgumentException("Volume must be at least input length.", nameof(volumes));
        for (var i = 0; i < prices.Length; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); if (!unitVolume) StreamingInputValidation.Finite(volumes[i], nameof(volumes)); }
        using var window = new MassThrustWindow(oscillator, MovingAvgType.ExponentialMovingAverage, length);
        for (var i = 0; i < prices.Length; i++) output[i] = window.Next(prices[i], unitVolume ? 1 : volumes[i], true).Value;
    }
    internal void Reset()
    { _advances.Reset(); _declines.Reset(); _upVolumes.Reset(); _downVolumes.Reset(); _signal.Reset(); _previousPrice = _previousLine = _previousSlope = default; _hasPrevious = false; }
    public void Dispose() => _signal.Dispose();
}
