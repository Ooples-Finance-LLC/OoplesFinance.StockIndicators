namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaSpecOptions : IIndicatorSpecOptions
{
    public EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaSpecOptions(int length = 13)
    { Length = Math.Max(1, length); }
    public int Length { get; }
}
