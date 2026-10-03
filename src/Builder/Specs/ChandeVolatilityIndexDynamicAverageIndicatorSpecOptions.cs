using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Volatility normalization period and adaptive gains.</summary>
public sealed class ChandeVolatilityIndexDynamicAverageIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ChandeVolatilityIndexDynamicAverageIndicatorSpecOptions(int length = 20, double alpha1 = .2, double alpha2 = .04,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        if (double.IsNaN(alpha1) || double.IsInfinity(alpha1) || alpha1 < 0) throw new ArgumentOutOfRangeException(nameof(alpha1));
        if (double.IsNaN(alpha2) || double.IsInfinity(alpha2) || alpha2 < 0) throw new ArgumentOutOfRangeException(nameof(alpha2));
        Length = Math.Max(1, length); Alpha1 = alpha1; Alpha2 = alpha2; MaType = maType;
    }
    public int Length { get; }
    public double Alpha1 { get; }
    public double Alpha2 { get; }
    public MovingAvgType MaType { get; }
}
