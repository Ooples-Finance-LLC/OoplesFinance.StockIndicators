using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>RSI periods and multipliers for the published QQE volatility widths.</summary>
public sealed class QuantitativeQualitativeEstimationSpecOptions : IIndicatorSpecOptions
{
    public QuantitativeQualitativeEstimationSpecOptions(int length = 14, int smoothLength = 5,
        double fastFactor = 2.618, double slowFactor = 4.236)
        : this(length, smoothLength, fastFactor, slowFactor, MovingAvgType.ExponentialMovingAverage) { }
    public QuantitativeQualitativeEstimationSpecOptions(int length, int smoothLength, double fastFactor, double slowFactor, MovingAvgType maType)
    {
        if (double.IsNaN(fastFactor) || double.IsInfinity(fastFactor) || fastFactor < 0) throw new ArgumentOutOfRangeException(nameof(fastFactor));
        if (double.IsNaN(slowFactor) || double.IsInfinity(slowFactor) || slowFactor < 0) throw new ArgumentOutOfRangeException(nameof(slowFactor));
        Length = Math.Max(1, length); SmoothLength = Math.Max(1, smoothLength);
        FastFactor = fastFactor; SlowFactor = slowFactor; MaType = maType;
    }
    public int Length { get; }
    public int SmoothLength { get; }
    public double FastFactor { get; }
    public double SlowFactor { get; }
    public MovingAvgType MaType { get; }
}
