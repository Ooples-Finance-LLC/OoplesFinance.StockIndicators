namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Price-change rank window for Retrospective Candlestick Chart.</summary>
public sealed class RetrospectiveCandlestickChartSpecOptions : IIndicatorSpecOptions
{
    public RetrospectiveCandlestickChartSpecOptions(int length = 100) { Length = Math.Max(1, length); }
    public int Length { get; }
}
