using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SellGravitationWindow : IDisposable
{
    private readonly Average _first;
    private readonly Average? _second;
    private Number _previousSpread;
    internal SellGravitationWindow(MovingAvgType kind, int length, bool includeSignal = true)
    { _first = new(kind, length); if (includeSignal) _second = new(kind, length); }
    private static Number Component(Number value)
    { var published = value.Publish(); return double.IsInfinity(published) ? value.Round(53) : Number.Of(published); }
    private static Number Body(double price, double open, double high, double low)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(open, nameof(open));
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var range = Number.Of(high) - Number.Of(low);
        return range.Sign == 0 ? default : (Number.Of(price) - Number.Of(open)).Divide(range);
    }
    private (double Line, double Signal, Signal Trade) Finish(Number first, Number second, bool final)
    {
        var spread = first - second; var change = spread - _previousSpread;
        var trade = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousSpread = spread;
        return (first.Publish(), second.Publish(), trade);
    }
    internal (double Line, double Signal, Signal Trade) Next(double price, double open, double high, double low, bool final)
    {
        var first = Component(_first.Next(Body(price, open, high, low), final));
        return Finish(first, _second?.Next(first, final) ?? default, final);
    }
    internal static (double[] Line, double[] Signal, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks, bool includeSignal = true)
    {
        length = Math.Max(1, length); var (prices, highs, lows, opens, _) = CalculationsHelper.GetInputValuesList(data);
        var body = new Number[prices.Count]; for (var i = 0; i < prices.Count; i++) body[i] = Body(prices[i], opens[i], highs[i], lows[i]);
        using var window = new SellGravitationWindow(kind, length, includeSignal);
        var line = new double[prices.Count]; var signal = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        {
            for (var i = 0; i < prices.Count; i++)
            { var first = Component(window._first.Next(body[i], true)); (line[i], signal[i], trades[i]) = window.Finish(first, window._second?.Next(first, true) ?? default, true); }
        }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                double[] Average(double[] values)
                {
                    foreach (var value in values) StreamingInputValidation.Finite(value, nameof(values));
                    var result = ComponentAverage.Take(values, length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); foreach (var value in result) StreamingInputValidation.Finite(value, nameof(result)); return result;
                }
                line = Average(body.Select(v => v.Publish()).ToArray());
                if (includeSignal) signal = Average(line);
                for (var i = 0; i < line.Length; i++) (_, _, trades[i]) = window.Finish(Number.Of(line[i]), Number.Of(signal[i]), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (line, signal, trades);
    }
    internal void Reset() { _first.Reset(); _second?.Reset(); _previousSpread = default; }
    public void Dispose() { Reset(); _first.Dispose(); _second?.Dispose(); }
}
