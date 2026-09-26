namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Options for Kendall tau-a between price and its rolling regression endpoint.</summary>
public sealed class KendallRankCorrelationCoefficientSpecOptions : IIndicatorSpecOptions
{
    public KendallRankCorrelationCoefficientSpecOptions(int length = 20) => Length = Math.Max(1, length);
    public int Length { get; }
}
