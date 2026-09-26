using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PriceZoneWindow : IDisposable
{
    private readonly RocBankAverage? _price, _directional;
    private readonly IMovingAverageSmoother? _priceFallback, _directionFallback;
    private bool _hasPrevious;
    private double _previous;
    internal PriceZoneWindow(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) { _price = new(kind, length, int.MaxValue); _directional = new(kind, length, int.MaxValue); }
        else { _priceFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _directionFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal static double Ratio(double directional, double price)
    {
        if (price == 0) return 0;
        var numerator = new ExactMeanAccumulator(); numerator.Add(directional, 100);
        var denominator = new ExactMeanAccumulator(); denominator.Add(price);
        return MathHelper.MinOrMax(numerator.Ratio(denominator), 100, -100);
    }
    internal double Next(double value, bool commit)
    {
        var signed = _hasPrevious ? value.CompareTo(_previous) * value : 0;
        var price = _price is null ? _priceFallback!.Next(value, commit) : _price.Next(new(value), commit).Publish();
        var directional = _directional is null ? _directionFallback!.Next(signed, commit) : _directional.Next(new(signed), commit).Publish();
        if (commit) { _previous = value; _hasPrevious = true; }
        return Ratio(directional, price);
    }
    internal void Reset() { _hasPrevious = false; _previous = 0; _price?.Reset(); _directional?.Reset(); _priceFallback?.Reset(); _directionFallback?.Reset(); }
    public void Dispose() { _price?.Dispose(); _directional?.Dispose(); _priceFallback?.Dispose(); _directionFallback?.Dispose(); }
}
