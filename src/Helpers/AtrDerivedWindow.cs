namespace OoplesFinance.StockIndicators.Helpers;

// Preserve the unpublished ATR exponent until the final scale or normalization.
internal sealed class AtrDerivedWindow : IDisposable
{
    private readonly KeltnerWindow _range;
    internal AtrDerivedWindow(int length, int capacityHint = int.MaxValue) => _range = new(MovingAvgType.ExponentialMovingAverage, 1, length, capacityHint: capacityHint);
    internal RocBankValue Next(double high, double low, double close, bool commit) => _range.Next(high, low, close, commit).Atr;
    internal static double Width(RocBankValue atr, double multiplier)
    {
        var product = new ExactMeanAccumulator(); product.AddProduct(atr.Mantissa, multiplier, 2); product.ScaleByPowerOfTwo(atr.UpperShift); return product.Mean(1);
    }
    internal static double Percent(RocBankValue atr, double close)
    {
        if (close == 0) return 0;
        var numerator = new ExactMeanAccumulator(); atr.AddTo(ref numerator, 100); var denominator = new ExactMeanAccumulator(); denominator.Add(close); return numerator.Ratio(denominator);
    }
    internal void Reset() => _range.Reset();
    public void Dispose() => _range.Dispose();
}
