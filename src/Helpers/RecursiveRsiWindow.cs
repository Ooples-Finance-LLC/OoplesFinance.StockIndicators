using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RecursiveRsiWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _source;
    private readonly Queue<double> _prices = new();
    private readonly Queue<Number> _outputs = new(), _midpoints = new();
    private readonly Queue<bool> _votes = new();
    private int _positive;
    private bool _hasSource;
    private Number _previousSource, _gain, _loss, _previous, _previousChange;
    internal RecursiveRsiWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _source = new(kind, _length); }
    private static Number Strength(Number gain, Number loss)
    {
        if (loss.Sign == 0) return Number.Of(100); if (gain.Sign == 0) return default;
        var total = gain + loss; if (total.Sign == 0) return default;
        var result = gain.Times(100).Divide(total);
        return result.Sign < 0 ? default : (result - Number.Of(100)).Sign > 0 ? Number.Of(100) : result;
    }
    private (double Value, Signal Trade) Finish(Number source, Number strength, bool final)
    {
        var ready = _outputs.Count == _length;
        var midpoint = (strength + (ready ? _outputs.Peek() : source)).Divide(2);
        var rising = (midpoint - (ready ? _midpoints.Peek() : default)).Sign >= 0;
        var output = ready ? Number.Integer(_positive).Times(100).Divide(_length) : Number.Of(rising ? 100 : 0);
        var change = output - _previous; var acceleration = change - _previousChange;
        var trade = change.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : change.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (ready) { _outputs.Dequeue(); _midpoints.Dequeue(); if (_votes.Dequeue()) _positive--; }
            _outputs.Enqueue(output); _midpoints.Enqueue(midpoint); _votes.Enqueue(rising); if (rising) _positive++;
            _previous = output; _previousChange = change;
        }
        return (output.Publish(), trade);
    }
    private (double Value, Signal Trade) FromSource(Number source, bool final)
    {
        var difference = _hasSource ? source - _previousSource : default;
        var gain = (_gain.Times(_length - 1L) + (difference.Sign > 0 ? difference : default)).Divide(_length);
        var loss = (_loss.Times(_length - 1L) + (difference.Sign < 0 ? default(Number) - difference : default)).Divide(_length);
        var result = Finish(source, Strength(gain, loss), final);
        if (final) { _previousSource = source; _hasSource = true; _gain = gain; _loss = loss; }
        return result;
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var change = _prices.Count == _length ? Number.Of(price) - Number.Of(_prices.Peek()) : default;
        var result = FromSource(_source.Next(change, final), final);
        if (final) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); }
        return result;
    }
    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var (prices, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new RecursiveRsiWindow(kind, length); var output = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (StrengthWindow.Supports(kind) && (!callbacks || !ComponentAverage.HasOverrides))
        { for (var i = 0; i < prices.Count; i++) (output[i], trades[i]) = window.Next(prices[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                double[] Average(double[] values, MovingAvgType averageKind)
                {
                    var result = (callbacks ? ComponentAverage.Take(values, length)?.ToArray() : null)
                        ?? CalculationsHelper.GetMovingAverageList(data, averageKind, length, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); foreach (var value in result) StreamingInputValidation.Finite(value, nameof(result)); return result;
                }
                var changes = new double[prices.Count];
                for (var i = length; i < prices.Count; i++) changes[i] = (Number.Of(prices[i]) - Number.Of(prices[i - length])).Publish();
                var source = Average(changes, kind);
                if (callbacks && ComponentAverage.HasOverrides)
                {
                    var gains = new double[prices.Count]; var losses = new double[prices.Count];
                    for (var i = 1; i < prices.Count; i++)
                    { var delta = Number.Of(source[i]) - Number.Of(source[i - 1]); if (delta.Sign > 0) gains[i] = delta.Publish(); else losses[i] = (default(Number) - delta).Publish(); }
                    var gain = Average(gains, MovingAvgType.WildersSmoothingMethod); var loss = Average(losses, MovingAvgType.WildersSmoothingMethod);
                    for (var i = 0; i < prices.Count; i++) (output[i], trades[i]) = window.Finish(Number.Of(source[i]), Strength(Number.Of(gain[i]), Number.Of(loss[i])), true);
                }
                else for (var i = 0; i < prices.Count; i++) (output[i], trades[i]) = window.FromSource(Number.Of(source[i]), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (output, trades);
    }
    internal void Reset()
    { _prices.Clear(); _outputs.Clear(); _midpoints.Clear(); _votes.Clear(); _source.Reset(); _positive = 0; _hasSource = false; _previousSource = _gain = _loss = _previous = _previousChange = default; }
    public void Dispose() { Reset(); _source.Dispose(); }
}
