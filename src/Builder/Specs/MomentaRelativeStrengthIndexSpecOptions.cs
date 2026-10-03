using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Options for RSI of distances from rolling price extremes.</summary>
public sealed class MomentaRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public MomentaRelativeStrengthIndexSpecOptions(int length1 = 2, int length2 = 14,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}
