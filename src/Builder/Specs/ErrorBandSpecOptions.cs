using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Period, envelope multiplier and center smoothing for MeanAbsoluteDeviationBands.</summary>
public sealed class MeanAbsoluteDeviationBandsSpecOptions : IIndicatorSpecOptions
{
    public MeanAbsoluteDeviationBandsSpecOptions(int length = 20, double stdDevFactor = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    { Length = Math.Max(1, length); StdDevFactor = stdDevFactor; MaType = maType; }
    public int Length { get; }
    public double StdDevFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Period, envelope multiplier and center smoothing for MeanAbsoluteErrorBands.</summary>
public sealed class MeanAbsoluteErrorBandsSpecOptions : IIndicatorSpecOptions
{
    public MeanAbsoluteErrorBandsSpecOptions(int length = 14, double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    { Length = Math.Max(1, length); StdDevFactor = stdDevFactor; MaType = maType; }
    public int Length { get; }
    public double StdDevFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Period, envelope multiplier and center smoothing for RootMovingAverageSquaredErrorBands.</summary>
public sealed class RootMovingAverageSquaredErrorBandsSpecOptions : IIndicatorSpecOptions
{
    public RootMovingAverageSquaredErrorBandsSpecOptions(int length = 14, double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    { Length = Math.Max(1, length); StdDevFactor = stdDevFactor; MaType = maType; }
    public int Length { get; }
    public double StdDevFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Nearest-rank quartile window and interquartile fence multiplier.</summary>
public sealed class InterquartileRangeBandsSpecOptions : IIndicatorSpecOptions
{
    public InterquartileRangeBandsSpecOptions(int length = 14, double mult = 1.5)
    { Length = Math.Max(1, length); Mult = mult; }
    public int Length { get; }
    public double Mult { get; }
}

/// <summary>Regression period for the forecast and cumulative absolute-error envelope.</summary>
public sealed class TimeSeriesForecastSpecOptions : IIndicatorSpecOptions
{
    public TimeSeriesForecastSpecOptions(int length = 500) => Length = Math.Max(1, length);
    public int Length { get; }
}
