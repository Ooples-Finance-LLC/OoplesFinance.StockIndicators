using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Flow window, signal period, and smoothing for volume positive/negative pressure.</summary>
public sealed class VolumePositiveNegativeIndicatorSpecOptions : IIndicatorSpecOptions
{
    public VolumePositiveNegativeIndicatorSpecOptions(int length = 30, int smoothLength = 3,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }
    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}
