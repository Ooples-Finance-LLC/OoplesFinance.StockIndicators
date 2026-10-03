using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Periods and smoothing for DiNapoliMovingAverageConvergenceDivergence.</summary>
public sealed class DiNapoliMovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public DiNapoliMovingAverageConvergenceDivergenceSpecOptions(double lc = 17.5185, double sc = 8.3896, double sp = 9.0503)
    {
        Helpers.RoundedFractionalEma.Coefficient(lc, nameof(lc));
        Helpers.RoundedFractionalEma.Coefficient(sc, nameof(sc));
        Helpers.RoundedFractionalEma.Coefficient(sp, nameof(sp));
        Lc = lc; Sc = sc; Sp = sp;
    }
    public double Lc { get; } public double Sc { get; } public double Sp { get; }
}

/// <summary>Periods and smoothing for ImpulseMovingAverageConvergenceDivergence.</summary>
public sealed class ImpulseMovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public ImpulseMovingAverageConvergenceDivergenceSpecOptions(int length = 34, int signalLength = 9, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    { Length = Math.Max(1, length); SignalLength = Math.Max(1, signalLength); MaType = maType; }
    public int Length { get; } public int SignalLength { get; } public MovingAvgType MaType { get; }
}

/// <summary>Periods and smoothing for MovingAverageConvergenceDivergenceLeader.</summary>
public sealed class MovingAverageConvergenceDivergenceLeaderSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageConvergenceDivergenceLeaderSpecOptions(int fastLength = 12, int slowLength = 26, int signalLength = 9, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); SignalLength = Math.Max(1, signalLength); MaType = maType; }
    public int FastLength { get; } public int SlowLength { get; } public int SignalLength { get; } public MovingAvgType MaType { get; }
}

/// <summary>Periods and smoothing for MirroredMovingAverageConvergenceDivergence.</summary>
public sealed class MirroredMovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public MirroredMovingAverageConvergenceDivergenceSpecOptions(int length = 20, int signalLength = 9, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { Length = Math.Max(1, length); SignalLength = Math.Max(1, signalLength); MaType = maType; }
    public int Length { get; } public int SignalLength { get; } public MovingAvgType MaType { get; }
}

