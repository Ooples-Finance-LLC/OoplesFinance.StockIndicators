namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Rolling high/low range used for Tirone's thirds and mean-based levels.</summary>
public sealed class TironeLevelsSpecOptions : IIndicatorSpecOptions
{
    public TironeLevelsSpecOptions(int length = 20) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Rolling high/low range projected outward by a quarter and half range.</summary>
public sealed class ProjectedSupportAndResistanceSpecOptions : IIndicatorSpecOptions
{
    public ProjectedSupportAndResistanceSpecOptions(int length = 25) => Length = Math.Max(1, length);
    public int Length { get; }
}
