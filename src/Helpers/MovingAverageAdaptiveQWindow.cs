using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MovingAverageAdaptiveQWindow
{
    private readonly int _length;
    private readonly Number _fast, _slow;
    private readonly Queue<(Number Price, Number Step)> _history = new();
    private Number _previousPrice, _travel, _anchor, _memory, _previousMargin;
    private bool _started;
    internal MovingAverageAdaptiveQWindow(int length, double fastAlpha, double slowAlpha)
    {
        StreamingInputValidation.Finite(fastAlpha, nameof(fastAlpha));
        StreamingInputValidation.Finite(slowAlpha, nameof(slowAlpha));
        _length = Math.Max(1, length); _fast = Number.Of(fastAlpha); _slow = Number.Of(slowAlpha);
    }
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = Number.Of(price);
        var step = _started ? Abs(current - _previousPrice) : default;
        var full = _history.Count == _length; var travel = _travel + step;
        if (full) travel -= _history.Peek().Step;
        var efficiency = full && travel.Sign > 0 ? Abs(current - _history.Peek().Price).Divide(travel) : default;
        // Adaptive Q adds slow to fast*ER; unlike KAMA, fast is not reduced by slow.
        var rate = _fast * efficiency + _slow; var gain = rate * rate;
        var distance = _started ? (current - _anchor) - _memory : default;
        var residual = (gain - Number.Of(1)) * distance; var mean = current + residual;
        var priceAnchor = (Abs(mean) - Abs(residual)).Sign >= 0;
        var anchor = priceAnchor ? current : default;
        var memory = (priceAnchor ? residual : mean).Round();
        var margin = default(Number) - residual; var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _history.Dequeue(); _history.Enqueue((current, step));
            _travel = travel; _previousPrice = current; _anchor = anchor; _memory = memory; _previousMargin = margin; _started = true;
        }
        return (mean.Publish(), trade);
    }
    internal void Reset()
    {
        _history.Clear(); _previousPrice = _travel = _anchor = _memory = _previousMargin = default; _started = false;
    }
}
