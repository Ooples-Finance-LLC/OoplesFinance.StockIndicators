namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>High-pass and low-pass periods for the original roofing indicator.</summary>
public sealed class EhlersRoofingFilterIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersRoofingFilterIndicatorSpecOptions(int length1 = 80, int length2 = 40)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; }
    public int Length2 { get; }
}
