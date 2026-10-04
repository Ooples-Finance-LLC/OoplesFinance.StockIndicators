using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VariableLengthWindow : IDisposable
{
    private readonly int _minimum, _maximum;
    private readonly Average _mean;
    private readonly Queue<Number> _prices = new();
    private Number _sum, _squares, _previous, _previousDifference;
    private int _period; private bool _started;
    internal VariableLengthWindow(MovingAvgType kind, int minimum, int maximum)
    { _minimum = Math.Max(1, minimum); _maximum = Math.Max(_minimum, maximum); _period = _maximum; _mean = new(kind, _maximum); }
    internal (double Value, int Length, Signal Trade) Next(double price, bool final, double? suppliedMean = null)
    {
        StreamingInputValidation.Finite(price, nameof(price)); if (suppliedMean is { } custom) StreamingInputValidation.Finite(custom, nameof(suppliedMean));
        var value = Number.Of(price); var mean = suppliedMean is { } supplied ? Number.Of(supplied) : _mean.Next(value, final);
        var full = _prices.Count == _maximum; var expired = full ? _prices.Peek() : default; var size = full ? _prices.Count : _prices.Count + 1;
        var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
        var variance = size < _maximum ? default : (squares.Times(size) - sum * sum).Divide(size).Divide(size);
        var period = _period;
        if (variance.Sign > 0)
        {
            var delta = value - mean; var distance = (delta * delta).Times(16);
            var step = (distance - variance).Sign <= 0 ? 1 : (distance - variance.Times(49)).Sign > 0 ? -1 : 0;
            period = (int)Math.Max(_minimum, Math.Min(_maximum, (long)_period + step));
        }
        var previous = _started ? _previous : value;
        var line = (previous.Times(period - 1L) + value.Times(2)).Divide(period + 1L);
        var difference = value - line; var change = difference - _previousDifference;
        var trade = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _prices.Dequeue(); _prices.Enqueue(value); _sum = sum; _squares = squares;
            _period = period; _previous = line; _previousDifference = difference; _started = true;
        }
        return (line.Publish(), period, trade);
    }
    internal static (double[] Values, double[] Lengths, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int minimum, int maximum)
    {
        minimum = Math.Max(1, minimum); maximum = Math.Max(minimum, maximum);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues; foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var custom = ComponentAverage.Take(prices.ToArray(), maximum); using var window = new VariableLengthWindow(kind, minimum, maximum);
        var values = new double[prices.Count]; var lengths = new double[prices.Count]; var signals = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        { var point = window.Next(prices[i], true, custom is null ? null : i < custom.Count ? custom[i] : 0); values[i] = point.Value; lengths[i] = point.Length; signals[i] = point.Trade; }
        return (values, lengths, signals);
    }
    internal void Reset() { _mean.Reset(); _prices.Clear(); _sum = _squares = _previous = _previousDifference = default; _period = _maximum; _started = false; }
    public void Dispose() { Reset(); _mean.Dispose(); }
}
