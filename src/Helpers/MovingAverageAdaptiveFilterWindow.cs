using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MovingAverageAdaptiveFilterWindow
{
    private readonly int _length;
    private readonly Number _filter, _fast, _slow;
    private readonly Queue<(Number Price, Number Step)> _prices = new();
    private readonly Queue<Number> _changes = new();
    private readonly EmaState _ema;
    private Number _previousPrice, _amaAnchor, _amaResidual, _travel, _sum, _squares, _previousVariance, _previousSlope;
    private bool _hasPrevious;
    internal MovingAverageAdaptiveFilterWindow(int length, double filter, double fastAlpha, double slowAlpha)
    {
        var options = new MovingAverageAdaptiveFilterSpecOptions(length, filter, fastAlpha, slowAlpha);
        _length = options.Length; _filter = Number.Of(filter); _fast = Number.Of(fastAlpha); _slow = Number.Of(slowAlpha);
        _ema = new EmaState(_length);
    }
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = Number.Of(price);
        var step = _hasPrevious ? Abs(current - _previousPrice) : default;
        var full = _prices.Count == _length; var travel = _travel + step;
        if (full) travel -= _prices.Peek().Step;
        var efficiency = full && travel.Sign > 0 ? Abs(current - _prices.Peek().Price).Divide(travel) : default;
        var alpha = _slow + (_fast - _slow) * efficiency; var gain = alpha * alpha;
        var distance = _hasPrevious ? (current - _amaAnchor) - _amaResidual : default;
        var increment = gain * distance;
        // Keep the smaller of the mean and its price-relative residual: a
        // constant-price tail needs the residual, but a huge excursion must
        // not erase a small mean while the gain is zero.
        var residual = increment - distance; var average = current + residual;
        var usePriceAnchor = (Abs(average) - Abs(residual)).Sign >= 0;
        var anchor = usePriceAnchor ? current : default;
        var memory = (usePriceAnchor ? residual : average).Round();
        var sum = _sum + increment; var squares = _squares + increment * increment;
        if (_changes.Count == _length) { var old = _changes.Peek(); sum -= old; squares -= old * old; }
        var variance = _changes.Count < _length - 1 ? default : (squares.Times(_length) - sum * sum).Divide(_length).Divide(_length);
        var value = _filter.Sign == 0 ? default : variance.OverRoot(variance) * _filter;
        // The original signal consumes the public seeded EMA, rounded once per stage.
        var slope = current - Number.Of(_ema.GetNext(price, final)); var change = slope - _previousSlope;
        var active = _filter.Sign == 0 || (variance - _previousVariance).Sign >= 0;
        var trade = !active ? Signal.None : slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _prices.Dequeue(); _prices.Enqueue((current, step));
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(increment);
            _travel = travel; _sum = sum; _squares = squares; _previousPrice = current; _amaAnchor = anchor; _amaResidual = memory;
            _previousVariance = variance; _previousSlope = slope; _hasPrevious = true;
        }
        return (value.Publish(), trade);
    }
    internal void Reset()
    {
        _prices.Clear(); _changes.Clear(); _ema.Reset();
        _previousPrice = _amaAnchor = _amaResidual = _travel = _sum = _squares = _previousVariance = _previousSlope = default; _hasPrevious = false;
    }
}
