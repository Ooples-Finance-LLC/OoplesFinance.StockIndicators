using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrueRangeAdjustedWindow
{
    private readonly int _length;
    private readonly Number _mult;
    private Number _sum, _average, _previousPrice, _line, _previousSlope;
    private long _count;
    private readonly double _multiplier;
    private readonly bool _allowCertified;
    private readonly List<(double Price, double High, double Low)>? _replay;
    private CertifiedTrueRangeAdjusted? _certified;
    internal long CertifiedCount { get; private set; }
    internal int FallbackCount { get; private set; }
    internal TrueRangeAdjustedWindow(int length, double mult) : this(length, mult, true) { }
    internal TrueRangeAdjustedWindow(int length, double mult, bool allowCertified)
    {
        StreamingInputValidation.Finite(mult, nameof(mult)); _length = Math.Max(1, length); _mult = Number.Of(mult); _multiplier = mult;
        _allowCertified = allowCertified && CertifiedTrueRangeAdjusted.Supports(_length, mult);
        if (_allowCertified) { _replay = new(); _certified = new(_length, mult); }
    }
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    internal (double Line, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        if (_certified is not null)
        {
            if (_certified.TryNext(price, high, low, final, out var result))
            {
                if (final) { _replay!.Add((price, high, low)); CertifiedCount++; }
                return result;
            }
            _certified = null; FallbackCount++;
            foreach (var bar in _replay!) NextExact(bar.Price, bar.High, bar.Low, true);
            _replay.Clear();
        }
        return NextExact(price, high, low, final);
    }
    private (double Line, Signal Trade) NextExact(double price, double high, double low, bool final)
    {
        var current = Number.Of(price); var previousPrice = _count == 0 ? current : _previousPrice;
        var range = Max(Number.Of(high) - Number.Of(low), Max(Abs(Number.Of(high) - previousPrice), Abs(Number.Of(low) - previousPrice)));
        var sum = _sum; Number average;
        if (_count < _length) { sum += range; average = sum.Divide(_count + 1); }
        else average = (_average.Times(_length - 1L) + range.Times(2)).Divide(_length + 1L);
        var ratio = average.Sign == 0 ? Number.Of(1) : range.Divide(average);
        var gain = ratio * _mult; if ((gain - Number.Of(2)).Sign > 0) gain = Number.Of(2);
        gain = gain.Times(2).Divide(_length + 1L);
        var line = _count == 0 ? current : _line + gain * (current - _line);
        var slope = line - _line; var acceleration = slope - _previousSlope;
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _count++; _sum = sum; _average = average; _previousPrice = current; _line = line; _previousSlope = slope; }
        return (line.Publish(), trade);
    }
    internal void Reset()
    {
        _count = 0; _sum = _average = _previousPrice = _line = _previousSlope = default;
        _replay?.Clear(); CertifiedCount = 0; FallbackCount = 0;
        _certified = _allowCertified ? new(_length, _multiplier) : null;
    }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data, int length, double mult)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var line = new double[prices.Count]; var trades = new Signal[prices.Count]; var window = new TrueRangeAdjustedWindow(length, mult);
        for (var i = 0; i < prices.Count; i++) (line[i], trades[i]) = window.Next(prices[i], data.HighPrices[i], data.LowPrices[i], true);
        return (line, trades);
    }
}
