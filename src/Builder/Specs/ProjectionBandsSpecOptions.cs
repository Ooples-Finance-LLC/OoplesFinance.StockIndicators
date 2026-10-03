namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Regression and envelope period for projection bands.</summary>
public sealed class ProjectionBandsSpecOptions : IIndicatorSpecOptions
{
    public ProjectionBandsSpecOptions(int length = 14) => Length = Math.Max(1, length);
    public int Length { get; }
}
