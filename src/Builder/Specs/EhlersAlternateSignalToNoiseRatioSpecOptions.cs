namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Signal-to-noise comparison level.</summary>
public sealed class EhlersAlternateSignalToNoiseRatioSpecOptions : IIndicatorSpecOptions
{
    public EhlersAlternateSignalToNoiseRatioSpecOptions(int length = 6) { Length = Math.Max(1, length); }
    public int Length { get; }
}
