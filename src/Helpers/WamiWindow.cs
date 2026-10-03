namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WamiWindow : IDisposable
{
    private readonly StrengthAverage _weighted, _first, _second;
    private double _previous;
    private bool _hasPrevious;
    internal WamiWindow(MovingAvgType kind, int length, int weightedLength = 4, int capacityHint = int.MaxValue)
    {
        _weighted = new(MovingAvgType.WeightedMovingAverage, weightedLength, capacityHint);
        _first = new(kind, length, capacityHint);
        _second = new(kind, length, capacityHint);
    }
    internal double Next(double price, bool final)
    {
        var difference = new ExactMeanAccumulator();
        if (_hasPrevious) { difference.Add(price); difference.Add(_previous, -1); }
        var value = _second.Next(_first.Next(_weighted.Next(StrengthValue.Round(difference, 1), final), final), final);
        if (final) { _previous = price; _hasPrevious = true; }
        return value.Doubled ? value.Mantissa * 2 : value.Mantissa;
    }
    internal static List<double> Compute(IReadOnlyList<double> prices, MovingAvgType kind, int length, int weightedLength)
    {
        using var window = new WamiWindow(kind, length, weightedLength, prices.Count);
        var result = new List<double>(prices.Count);
        foreach (var price in prices) result.Add(window.Next(price, true));
        return result;
    }
    internal void Reset() { _weighted.Reset(); _first.Reset(); _second.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() { _weighted.Dispose(); _first.Dispose(); _second.Dispose(); }
}
