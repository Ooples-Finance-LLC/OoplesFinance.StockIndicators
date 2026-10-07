using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class McGinleyWindow
{
    private readonly Number _coefficient;
    private double _previous;
    private bool _started;
    internal McGinleyWindow(int length, double factor)
    { StreamingInputValidation.Finite(factor, nameof(factor)); _coefficient = Number.Of(factor).Times(Math.Max(1, length)); }
    internal double Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var result = price;
        if (_started && _previous != 0 && _coefficient.Sign > 0)
        {
            var current = Number.Of(price); var prior = Number.Of(_previous);
            var priceSquared = current * current; var priorSquared = prior * prior;
            var q = _coefficient * priceSquared * priceSquared; var v = priorSquared * priorSquared;
            // q/v is the public denominator. Below its floor the result is price.
            // Otherwise the update is a convex combination, without a price ratio
            // or an overflowing current-minus-prior intermediate.
            if ((q - v).Sign > 0) result = (prior * (q - v) + current * v).Divide(q).Publish();
        }
        // The public recurrence feeds its published binary64 output into the next step.
        if (final) { _previous = result; _started = true; }
        return result;
    }
    internal void Reset() { _previous = 0; _started = false; }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, int length, double factor)
    {
        var window = new McGinleyWindow(length, factor);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues, data.InputValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var values = new double[input.Count]; var signals = new Signal[input.Count]; Number previousMargin = default;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = window.Next(input[i], true); var margin = Number.Of(input[i]) - Number.Of(values[i]); var change = margin - previousMargin;
            signals[i] = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previousMargin = margin;
        }
        return (values, signals);
    }
}
