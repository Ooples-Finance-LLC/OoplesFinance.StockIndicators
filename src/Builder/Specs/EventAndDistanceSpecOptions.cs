using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Periods and distance scaling for DrunkardWalk.</summary>
public sealed class DrunkardWalkSpecOptions : IIndicatorSpecOptions
{
    public DrunkardWalkSpecOptions(int length1 = 80, int length2 = 14)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; } public int Length2 { get; }
}

/// <summary>Periods and distance scaling for WellesWilderVolatilitySystem.</summary>
public sealed class WellesWilderVolatilitySystemSpecOptions : IIndicatorSpecOptions
{
    public WellesWilderVolatilitySystemSpecOptions(int length1 = 63, int length2 = 21, double factor = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Factor = factor; MaType = maType; }
    public int Length1 { get; } public int Length2 { get; } public double Factor { get; } public MovingAvgType MaType { get; }
}

/// <summary>Periods and distance scaling for UtBotAlerts.</summary>
public sealed class UtBotAlertsSpecOptions : IIndicatorSpecOptions
{
    public UtBotAlertsSpecOptions(int length = 10, double keyValue = 1, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    { Length = Math.Max(1, length); KeyValue = keyValue; MaType = maType; }
    public int Length { get; } public double KeyValue { get; } public MovingAvgType MaType { get; }
}

/// <summary>Periods and distance scaling for ZDistanceFromVwap.</summary>
public sealed class ZDistanceFromVwapSpecOptions : IIndicatorSpecOptions
{
    public ZDistanceFromVwapSpecOptions(int length = 20, MovingAvgType maType = MovingAvgType.VolumeWeightedAveragePrice)
    { Length = Math.Max(1, length); MaType = maType; }
    public int Length { get; } public MovingAvgType MaType { get; }
}

