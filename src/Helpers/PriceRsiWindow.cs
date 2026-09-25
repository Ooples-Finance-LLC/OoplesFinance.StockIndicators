namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PriceRsiWindow : IDisposable
{
    private readonly StrengthAverage _gains, _losses;
    private readonly bool _preserveFlat;
    private double _previous, _line;
    private bool _hasPrevious;
    internal PriceRsiWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _gains = new(kind, length, capacityHint); _losses = new(kind, length, capacityHint);
        _preserveFlat = length > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    }
    internal double Next(double price, bool final)
    {
        var change = _hasPrevious ? GainLossShare.Change(price, _previous) : default;
        var gain = _gains.Next(change.Mantissa > 0 ? change : default, final);
        var loss = _losses.Next(change.Mantissa < 0 ? change.Absolute : default, final);
        var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); gain.AddTo(ref total); loss.AddTo(ref total);
        var value = _preserveFlat && _hasPrevious && price == _previous ? _line : GainLossShare.Of(numerator, total, 100);
        if (final) { _previous = price; _line = value; _hasPrevious = true; }
        return value;
    }
    internal void Reset() { _gains.Reset(); _losses.Reset(); _previous = _line = 0; _hasPrevious = false; }
    public void Dispose() { _gains.Dispose(); _losses.Dispose(); }
}
