using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Raw-MAD outlier replacement followed by a zero-seeded EMA, as published.
internal sealed class HampelWindow : IDisposable
{
    private readonly int _length;
    private readonly Number _factor;
    private readonly Queue<Number> _history = new();
    private Number _previous;
    internal HampelWindow(int length, double factor)
    { StreamingInputValidation.Finite(factor, nameof(factor)); _length = Math.Max(1, length); _factor = Number.Of(factor); }
    private static Number Abs(Number value) => value.Sign < 0 ? value.Times(-1) : value;
    private static Number Median(Number[] sorted) => (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]).Divide(2);
    internal Number NextExact(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = Number.Of(price);
        var expired = _history.Count == _length;
        var window = _history.Skip(expired ? 1 : 0).Concat(new[] { current }).ToArray();
        Array.Sort(window, (a, b) => (a - b).Sign); var median = Median(window);
        var deviations = window.Select(v => Abs(v - median)).ToArray();
        Array.Sort(deviations, (a, b) => (a - b).Sign); var mad = Median(deviations);
        var filtered = (Abs(current - median) - _factor * mad).Sign <= 0 ? current : median;
        var result = (_previous.Times(_length - 1L) + filtered.Times(2)).Divide(_length + 1L);
        if (final) { if (expired) _history.Dequeue(); _history.Enqueue(current); _previous = result; }
        return result;
    }
    internal double Next(double price, bool final) => NextExact(price, final).Publish();
    internal void Reset() { _history.Clear(); _previous = default; }
    public void Dispose() => Reset();
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, int length, double factor)
    {
        using var window = new HampelWindow(length, factor);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues, data.InputValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var values = new double[prices.Count]; var signals = new Signal[prices.Count]; Number previousMargin = default;
        for (var i = 0; i < values.Length; i++)
        {
            var value = window.NextExact(prices[i], true); values[i] = value.Publish();
            var margin = Number.Of(prices[i]) - value; var movement = margin - previousMargin;
            signals[i] = margin.Sign > 0 && movement.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && movement.Sign < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previousMargin = margin;
        }
        return (values, signals);
    }
}
