using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Window of accumulated high and low prices for closed-form distance volatility.</summary>
public sealed class ClosedFormDistanceVolatilitySpecOptions : IIndicatorSpecOptions
{
    public ClosedFormDistanceVolatilitySpecOptions(int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { Length = Math.Max(1, length); MaType = maType; }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Window of price and price-change dispersion.</summary>
public sealed class MotionSmoothnessIndexSpecOptions : IIndicatorSpecOptions
{
    public MotionSmoothnessIndexSpecOptions(int length = 50) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Regression window for changes in fitted log prices.</summary>
public sealed class NaturalMarketSlopeSpecOptions : IIndicatorSpecOptions
{
    public NaturalMarketSlopeSpecOptions(int length = 40) => Length = Math.Max(1, length);
    public int Length { get; }
}
