namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Median-phase window for the cycle-tuned RSI.</summary>
public sealed class DominantCycleTunedRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public DominantCycleTunedRelativeStrengthIndexSpecOptions(int length = 5) => Length = Math.Max(1, length);
    public int Length { get; }
}
