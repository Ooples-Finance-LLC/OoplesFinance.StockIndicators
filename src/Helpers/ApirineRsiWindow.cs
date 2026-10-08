namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ApirineRsiWindow : IDisposable
{
    private readonly RocBankAverage _price, _gain, _loss;
    private readonly bool _wilder;
    private readonly double _retention;
    private RocBankValue _residual;
    private double _previousPrice;
    internal ApirineRsiWindow(MovingAvgType kind, int length, int smoothLength, int capacityHint = int.MaxValue)
    {
        _price = new(kind, smoothLength, capacityHint); _gain = new(kind, length, capacityHint); _loss = new(kind, length, capacityHint);
        _wilder = kind == MovingAvgType.WildersSmoothingMethod;
        _retention = 1 - 1d / Math.Max(1, smoothLength);
    }
    internal double Next(double price, bool final)
    {
        var mean = _price.Next(new RocBankValue(price), final);
        var difference = new ExactMeanAccumulator(); difference.Add(price);
        RocBankValue residual;
        if (_wilder)
        {
            difference.Add(_previousPrice, -1);
            var change = RocBankValue.Round(difference);
            var total = new ExactMeanAccumulator(); _residual.AddTo(ref total); change.AddTo(ref total);
            var combined = RocBankValue.Round(total);
            // An extended value has a normal mantissa; multiplying by retention cannot underflow it.
            residual = new RocBankValue(_retention * combined.Mantissa, combined.UpperShift);
        }
        else { mean.AddTo(ref difference, -1); residual = RocBankValue.Round(difference); }
        var up = _gain.Next(residual.Mantissa > 0 ? residual : default, final);
        var down = _loss.Next(residual.Mantissa < 0 ? new RocBankValue(-residual.Mantissa, residual.UpperShift) : default, final);
        var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100);
        var denominator = new ExactMeanAccumulator(); up.AddTo(ref denominator); down.AddTo(ref denominator);
        if (final) { _residual = residual; _previousPrice = price; }
        return GainLossShare.Of(numerator, denominator, 100);
    }
    internal void Reset() { _price.Reset(); _gain.Reset(); _loss.Reset(); _residual = default; _previousPrice = 0; }
    public void Dispose() { _price.Dispose(); _gain.Dispose(); _loss.Dispose(); }
}
