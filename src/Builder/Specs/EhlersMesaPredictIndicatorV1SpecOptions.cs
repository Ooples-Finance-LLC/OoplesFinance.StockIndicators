namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Forecast horizon, Burg order, Hann smoothing and fitting window for MESA prediction.</summary>
public sealed class EhlersMesaPredictIndicatorV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersMesaPredictIndicatorV1SpecOptions(int length1 = 5, int length2 = 4, int lowerLength = 12, int upperLength = 54)
    {
        Length1 = Math.Max(1, length1); UpperLength = Math.Max(2, upperLength);
        Length2 = Math.Min(Math.Max(1, length2), UpperLength-1); LowerLength = Math.Max(1, lowerLength);
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int LowerLength { get; }
    public int UpperLength { get; }
}
