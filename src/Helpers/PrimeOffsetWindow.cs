using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PrimeOffsetWindow
{
    private readonly int _tolerance;
    private long _upper, _lower;
    private double _previous;
    internal PrimeOffsetWindow(int tolerance) => _tolerance = Math.Max(1, tolerance);
    internal double Next(double value, bool final)
    {
        var found = PrimeNumberSearch.Find(value, _tolerance);
        var upper = found.Upper == 0 ? _upper : found.Upper;
        var lower = found.Lower == 0 ? _lower : found.Lower;
        var price = F.Of(value); var up = (F)upper - price; var down = price - (F)lower;
        var offset = up < down ? up : -down;
        var output = offset.Sign == 0 ? _previous : offset.Publish();
        if (final) { _upper = upper; _lower = lower; _previous = output; }
        return output;
    }
    internal void Reset() { _upper = _lower = 0; _previous = 0; }
    internal static void Validate(OhlcvBar bar)
    {
        StreamingInputValidation.Validate(bar);
        PrimeNumberSearch.Validate(bar.Open); PrimeNumberSearch.Validate(bar.High);
        PrimeNumberSearch.Validate(bar.Low); PrimeNumberSearch.Validate(bar.Close);
    }
    internal static void Validate(StockData data)
    {
        foreach (var values in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices })
            foreach (var value in values) PrimeNumberSearch.Validate(value);
        var (input, _, _, _, volume) = CalculationsHelper.GetInputValuesList(data);
        foreach (var value in input) PrimeNumberSearch.Validate(value);
        foreach (var value in volume) StreamingInputValidation.Finite(value, nameof(data));
    }
}
internal sealed class PrimeBandWindow
{
    private readonly PrimeOffsetWindow _upper, _lower;
    private readonly int _width;
    private long _index;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    internal PrimeBandWindow(int length) { _upper = new(length); _lower = new(length); _width = Math.Max(2, length); }
    private double Extreme(LinkedList<(long Index, double Value)> q, double value, bool high, bool final)
    {
        var first = q.First;
        while (first is not null && first.Value.Index <= _index - _width) first = first.Next;
        var result = first is null ? value : high ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (q.First is { } old && old.Value.Index <= _index - _width) q.RemoveFirst();
            while (q.Last is { } last && (high ? last.Value.Value <= value : last.Value.Value >= value)) q.RemoveLast();
            q.AddLast((_index, value));
        }
        return result;
    }
    internal (double Upper, double Lower) Next(double high, double low, bool final)
    {
        PrimeNumberSearch.Validate(high); PrimeNumberSearch.Validate(low);
        var up = _upper.Next(high, final); var down = _lower.Next(low, final);
        var result = (Extreme(_high, up, true, final), Extreme(_low, down, false, final));
        if (final) _index++;
        return result;
    }
    internal void Reset() { _upper.Reset(); _lower.Reset(); _high.Clear(); _low.Clear(); _index = 0; }
}
