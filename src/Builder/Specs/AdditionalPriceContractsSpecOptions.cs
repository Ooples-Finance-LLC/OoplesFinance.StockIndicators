using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Window for the root-mean-square minus arithmetic-mean spread.</summary>
public sealed class QmaSmaDifferenceSpecOptions : IIndicatorSpecOptions
{
    public QmaSmaDifferenceSpecOptions(int length = 14) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Average period, backward displacement and percentage envelope width.</summary>
public sealed class MovingAverageDisplacedEnvelopeSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageDisplacedEnvelopeSpecOptions(int length1 = 9, int length2 = 13, double pct = 0.5,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Pct = pct; MaType = maType; }
    public int Length1 { get; }
    public int Length2 { get; }
    public double Pct { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Average and range period with a multiplier for the full rolling range.</summary>
public sealed class RangeBandsSpecOptions : IIndicatorSpecOptions
{
    public RangeBandsSpecOptions(int length = 14, double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    { Length = Math.Max(1, length); StdDevFactor = stdDevFactor; MaType = maType; }
    public int Length { get; }
    public double StdDevFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>Retained legacy period; containment of each close determines range transitions.</summary>
public sealed class RangeIdentifierSpecOptions : IIndicatorSpecOptions
{
    public RangeIdentifierSpecOptions(int length = 34) => Length = Math.Max(1, length);
    public int Length { get; }
}
