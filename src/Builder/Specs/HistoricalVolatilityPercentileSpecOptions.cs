using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Log-residual period and annual volatility rank window.</summary>
public sealed class HistoricalVolatilityPercentileSpecOptions : IIndicatorSpecOptions
{
    public HistoricalVolatilityPercentileSpecOptions(int length = 21, int annualLength = 252)
        : this(length, annualLength, MovingAvgType.ExponentialMovingAverage) { }
    public HistoricalVolatilityPercentileSpecOptions(int length, int annualLength, MovingAvgType maType)
    { Length = Math.Max(1, length); AnnualLength = Math.Max(1, annualLength); MaType = maType; }
    public int Length { get; }
    public int AnnualLength { get; }
    public MovingAvgType MaType { get; }
}
