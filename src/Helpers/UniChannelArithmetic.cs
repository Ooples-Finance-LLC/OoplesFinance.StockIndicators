namespace OoplesFinance.StockIndicators.Helpers;
internal static class UniChannelArithmetic
{
    internal static void Validate(double upper, double lower) { HighLowBandsWindow.ValidateShift(upper); HighLowBandsWindow.ValidateShift(lower); }
    internal static double Band(double middle, double factor, bool additive)
    { var sum = new ExactMeanAccumulator(); sum.Add(middle); if (additive) sum.Add(factor); else sum.AddProduct(middle, factor); return sum.Mean(1); }
}
