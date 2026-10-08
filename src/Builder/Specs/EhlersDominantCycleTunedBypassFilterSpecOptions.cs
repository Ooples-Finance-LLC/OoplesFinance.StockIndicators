namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Cycle limits, high-pass cutoff, and median period for the spectrum filter bank.</summary>
public sealed class EhlersDominantCycleTunedBypassFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersDominantCycleTunedBypassFilterSpecOptions(int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10)
    {
        MinLength = Math.Max(3, minLength);
        MaxLength = Math.Max(MinLength, maxLength);
        Length1 = Math.Max(3, length1);
        Length2 = Math.Max(1, length2);
    }
    public int MinLength { get; }
    public int MaxLength { get; }
    public int Length1 { get; }
    public int Length2 { get; }
}
