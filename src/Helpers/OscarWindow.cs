using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class OscarWindow : IDisposable
{
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count;
    private Number _previous, _previousSlope;
    internal OscarWindow(int length) => _length = Math.Max(1, length);
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _length + 1L;
        var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal (double Value, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var highest = Extreme(_highs, high, true, final); var lowest = Extreme(_lows, low, false, final);
        var range = Number.Of(highest) - Number.Of(lowest);
        var rough = range.Sign == 0 ? default : (Number.Of(price) - Number.Of(lowest)).Divide(range).Times(100);
        if (rough.Sign < 0) rough = default;
        if ((rough - Number.Of(100)).Sign > 0) rough = Number.Of(100);
        var value = _previous.Divide(6) + rough.Divide(3);
        var slope = value - _previous; var acceleration = slope - _previousSlope;
        // With zero initialization and clamped rough in [0,100], Oscar stays in [0,40].
        // The threshold crossings in GetRsiSignal imply the same slope direction.
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
            : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previous = value; _previousSlope = slope; _count++; }
        return (value.Publish(), trade);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _count = 0; _previous = _previousSlope = default; }
    public void Dispose() => Reset();
}
