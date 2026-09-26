using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Periods, smoothing and signal multipliers for the four-oscillator combination.</summary>
public sealed class _4MovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public _4MovingAverageConvergenceDivergenceSpecOptions(int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4); Length5 = Math.Max(1, length5); Length6 = Math.Max(1, length6);
        BlueMult = blueMult; YellowMult = yellowMult; MaType = maType;
    }
    public int Length1 { get; } public int Length2 { get; } public int Length3 { get; }
    public int Length4 { get; } public int Length5 { get; } public int Length6 { get; }
    public double BlueMult { get; } public double YellowMult { get; } public MovingAvgType MaType { get; }
}

/// <summary>Periods, smoothing and signal multipliers for the four-oscillator combination.</summary>
public sealed class _4PercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public _4PercentagePriceOscillatorSpecOptions(int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4); Length5 = Math.Max(1, length5); Length6 = Math.Max(1, length6);
        BlueMult = blueMult; YellowMult = yellowMult; MaType = maType;
    }
    public int Length1 { get; } public int Length2 { get; } public int Length3 { get; }
    public int Length4 { get; } public int Length5 { get; } public int Length6 { get; }
    public double BlueMult { get; } public double YellowMult { get; } public MovingAvgType MaType { get; }
}

/// <summary>MACD periods and multiplier for explosion trend strength.</summary>
public sealed class WaddahAttarExplosionSpecOptions : IIndicatorSpecOptions
{
    public WaddahAttarExplosionSpecOptions(int fastLength = 20, int slowLength = 40, double sensitivity = 150)
    { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); Sensitivity = sensitivity; }
    public int FastLength { get; } public int SlowLength { get; } public double Sensitivity { get; }
}

/// <summary>Breakout window used to count directional trend events.</summary>
public sealed class TrendForceHistogramSpecOptions : IIndicatorSpecOptions
{
    public TrendForceHistogramSpecOptions(int length = 14) => Length = Math.Max(1, length);
    public int Length { get; }
}
