using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Lookback and smoothing for the advancing fraction of price changes.</summary>
public sealed class ZweigMarketBreadthIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ZweigMarketBreadthIndicatorSpecOptions(int length = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { Length = Math.Max(1, length); MaType = maType; }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Lookback for time elapsed since a new high or low.</summary>
public sealed class TimePriceIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TimePriceIndicatorSpecOptions(int length = 50) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Lookback and average used to normalize candle direction by its range.</summary>
public sealed class NormalizedRelativeVigorIndexSpecOptions : IIndicatorSpecOptions
{
    public NormalizedRelativeVigorIndexSpecOptions(int length = 10, MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage)
    { Length = Math.Max(1, length); MaType = maType; }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}
