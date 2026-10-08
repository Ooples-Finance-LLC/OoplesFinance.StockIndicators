namespace OoplesFinance.StockIndicators.Helpers;
internal static class RangeChannelWindow
{
    private static RocBankValue RoundedBoundary(double price, RocBankValue atr, double multiplier, int sign)
    {
        var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, multiplier, sign); sum.ScaleByPowerOfTwo(atr.UpperShift); sum.Add(price);
        var value = RocBankValue.Round(sum);
        // An extended binary64 value already has an integer spacing larger than one.
        return value.UpperShift == 0 ? new RocBankValue(Math.Round(value.Mantissa)) : value;
    }
    internal static (double Upper, double Middle, double Lower, double Average) Output(double price, RocBankValue average, RocBankValue atr, double multiplier, bool rounded)
    {
        if (!rounded) { var bands = KeltnerWindow.Bands(average, atr, multiplier); return (bands.Upper, bands.Middle, bands.Lower, bands.Middle); }
        var upper = RoundedBoundary(price, atr, multiplier, 1); var lower = RoundedBoundary(price, atr, multiplier, -1);
        var sum = new ExactMeanAccumulator(); upper.AddTo(ref sum); lower.AddTo(ref sum);
        return (upper.Publish(), sum.Mean(2), lower.Publish(), average.Publish());
    }
}
