using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SwamiWindow : IDisposable
{
    private readonly long _width;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index;
    private Number _numerator, _denominator, _previous, _previousSlope;
    internal SwamiWindow(int fastLength, int slowLength)
        => _width = Math.Max(1L, (long)Math.Max(1, slowLength) - Math.Max(1, fastLength));
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _width + 1;
        var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null || (maximum ? value > first.Value.Value : value < first.Value.Value) ? value : first.Value.Value;
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    internal (double Line, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var highest = Number.Of(Extreme(_highs, high, true, final)); var lowest = Number.Of(Extreme(_lows, low, false, final));
        var numerator = (Number.Of(price) - lowest + _numerator).Divide(2);
        var denominator = (highest - lowest + _denominator).Divide(2);
        // The fixed smoothing coefficients are exactly one fifth and four fifths.
        var line = denominator.Sign == 0 ? default : (numerator.Divide(denominator) + _previous.Times(4)).Divide(5);
        if (line.Sign < 0) line = default;
        if ((line - Number.Of(1)).Sign > 0) line = Number.Of(1);
        var slope = line - _previous; var acceleration = slope - _previousSlope;
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
            : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _numerator = numerator; _denominator = denominator; _previous = line; _previousSlope = slope; _index++; }
        return (line.Publish(), trade);
    }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data, int fastLength, int slowLength)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var line = new double[prices.Count]; var trades = new Signal[prices.Count]; using var window = new SwamiWindow(fastLength, slowLength);
        for (var i = 0; i < prices.Count; i++) (line[i], trades[i]) = window.Next(prices[i], data.HighPrices[i], data.LowPrices[i], true);
        return (line, trades);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _index = 0; _numerator = _denominator = _previous = _previousSlope = default; }
    public void Dispose() => Reset();
}
