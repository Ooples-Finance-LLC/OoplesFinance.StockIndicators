namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Prime search percentage and trailing extrema period.</summary>
public sealed class PrimeNumberBandsSpecOptions : IIndicatorSpecOptions
{
    public PrimeNumberBandsSpecOptions(int length = 5) => Length = Math.Max(1, length);
    public int Length { get; }
}
