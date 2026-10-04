using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RetentionAccelerationWindow
{
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _high1 = new(), _low1 = new(), _high2 = new(), _low2 = new();
    private long _index;
    private double _previous;
    private F _previousMargin;
    internal RetentionAccelerationWindow(int length) => _length = Math.Max(1, length);
    private double Extreme(LinkedList<(long Index, double Value)> q, double value, long width, bool maximum)
    {
        var first = q.First;
        while (first is not null && first.Value.Index <= _index - width) first = first.Next;
        return first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
    }
    private void Commit(LinkedList<(long Index, double Value)> q, double value, long width, bool maximum)
    {
        while (q.First is { } first && first.Value.Index <= _index - width) q.RemoveFirst();
        while (q.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) q.RemoveLast();
        q.AddLast((_index, value));
    }
    internal (double Value, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        StreamingInputValidation.Finite(high, nameof(high));
        StreamingInputValidation.Finite(low, nameof(low));
        var h1 = F.Of(Extreme(_high1, high, _length, true));
        var h2 = F.Of(Extreme(_high2, high, 2L * _length, true));
        var a = 2 * (h1 - F.Of(Extreme(_low1, low, _length, false)));
        var b = 2 * (h2 - F.Of(Extreme(_low2, low, 2L * _length, false)));
        double ratio = 0;
        // Preserve every exact zero-r1 case before cancelling the intermediate divisions.
        if (h1.Sign > 0 && a.Sign != 0 && b.Sign != 0 && a.CompareTo((F)1) != 0 && b.CompareTo((F)1) != 0 && a.CompareTo(b) != 0)
        {
            var square = h2 * a * a / (h1 * b * b);
            var sign = a.Sign * b.Sign;
            ratio = sign > 0 && square >= (F)1 ? 1
                : sign * ExactPopulationDeviation.RootRatio(square.Numerator << 2148, square.Denominator);
        }
        var gain = F.Of(Math.Pow(ratio, Math.Sqrt(_length)) / _length);
        var current = F.Of(price); var previous = F.Of(_index == 0 ? price : _previous);
        var value = (previous + gain * (current - previous)).Publish();
        var margin = current - F.Of(value); var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            Commit(_high1, high, _length, true); Commit(_low1, low, _length, false);
            Commit(_high2, high, 2L * _length, true); Commit(_low2, low, 2L * _length, false);
            _previous = value; _previousMargin = margin; _index++;
        }
        return (value, trade);
    }
    internal void Reset() { _high1.Clear(); _low1.Clear(); _high2.Clear(); _low2.Clear(); _index = 0; _previous = 0; _previousMargin = default; }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, int length)
    {
        var (input, highs, lows, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var state = new RetentionAccelerationWindow(length); var output = new double[input.Count]; var signals = new Signal[input.Count];
        for (var i = 0; i < input.Count; i++) { var next = state.Next(input[i], highs[i], lows[i], true); output[i] = next.Value; signals[i] = next.Trade; }
        return (output, signals);
    }
}
