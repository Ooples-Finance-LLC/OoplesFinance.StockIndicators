using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MobilityWindow : IDisposable
{
    private readonly int _bins, _length;
    private readonly Queue<(BigInteger Low, BigInteger High)> _candles = new();
    private readonly Queue<BigInteger> _prices = new();
    private readonly UnroundedMovingAverage _line, _signal;
    private Number _previousDifference;

    internal MobilityWindow(MovingAvgType kind, int bins, int length, int smooth)
    {
        _bins = Math.Max(1, bins); _length = Math.Max(1, length);
        _line = new(kind, smooth); _signal = new(kind, smooth);
    }

    internal static void Validate(double high, double low, double price)
    {
        StreamingInputValidation.Finite(high, nameof(high));
        StreamingInputValidation.Finite(low, nameof(low));
        StreamingInputValidation.Finite(price, nameof(price));
        if (low > high) throw new ArgumentOutOfRangeException(nameof(low), "Low must not exceed high.");
    }

    private Number Raw(double high, double low, double price, bool final)
    {
        Validate(high, low, price);
        var candle = (Low: ExactVarianceWindow.Units(low), High: ExactVarianceWindow.Units(high));
        var comparison = ExactVarianceWindow.Units(price);
        var full = _prices.Count == _length;
        Number raw = default;
        if (full)
        {
            // The comparison is one bar older than the candle window.
            var sample = _candles.Skip(1).ToList(); sample.Add(candle);
            raw = MobilityDensity.Evaluate(sample, _prices.Peek(), _bins);
        }
        if (final)
        {
            if (full) { _candles.Dequeue(); _prices.Dequeue(); }
            _candles.Enqueue(candle); _prices.Enqueue(comparison);
        }
        return raw;
    }

    private Signal Trade(Number line, Number signal, bool final)
    {
        var difference = line - signal; var change = difference - _previousDifference;
        var trade = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy
            : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousDifference = difference;
        return trade;
    }

    internal (double Line, double SignalLine, Signal Trade) Next(double high, double low, double price, bool final)
    {
        var raw = Raw(high, low, price, final);
        var line = _line.Next(raw, final); var signal = _signal.Next(line, final);
        return (line.Publish(), signal.Publish(), Trade(line, signal, final));
    }

    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(
        StockData data, MovingAvgType kind, int bins, int length, int smooth)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        for (var i = 0; i < prices.Count; i++) Validate(data.HighPrices[i], data.LowPrices[i], prices[i]);
        using var window = new MobilityWindow(kind, bins, length, smooth);
        var line = new double[prices.Count]; var signal = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!ComponentAverage.HasOverrides && StrengthWindow.Supports(kind))
        {
            for (var i = 0; i < prices.Count; i++)
                (line[i], signal[i], trades[i]) = window.Next(data.HighPrices[i], data.LowPrices[i], prices[i], true);
        }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                var raw = new double[prices.Count];
                for (var i = 0; i < raw.Length; i++) raw[i] = window.Raw(data.HighPrices[i], data.LowPrices[i], prices[i], true).Publish();
                smooth = Math.Max(1, smooth);
                line = ComponentAverage.Take(raw, smooth)?.ToArray()
                    ?? CalculationsHelper.GetMovingAverageList(data, kind, smooth, raw.ToList()).ToArray();
                data.RestoreInputSeries(caller);
                signal = ComponentAverage.Take(line, smooth)?.ToArray()
                    ?? CalculationsHelper.GetMovingAverageList(data, kind, smooth, line.ToList()).ToArray();
                for (var i = 0; i < prices.Count; i++) trades[i] = window.Trade(Number.Of(line[i]), Number.Of(signal[i]), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (line, signal, trades);
    }

    internal void Reset()
    { _candles.Clear(); _prices.Clear(); _line.Reset(); _signal.Reset(); _previousDifference = default; }
    public void Dispose() { Reset(); _line.Dispose(); _signal.Dispose(); }
}
