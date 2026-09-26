namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Range lookback and smoothing period for the Ehlers Hurst coefficient.</summary>
public sealed class EhlersHurstCoefficientSpecOptions : IIndicatorSpecOptions
{
    public EhlersHurstCoefficientSpecOptions(int length1 = 30, int length2 = 20)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; }
    public int Length2 { get; }
}
