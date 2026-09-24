namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Double-stochastic Schaff trend-cycle periods.</summary>
public sealed class SchaffTrendCycleShkSpecOptions : IIndicatorSpecOptions
{
    public SchaffTrendCycleShkSpecOptions(int fastLength = 23, int slowLength = 50, int cycleLength = 10, int d1Length = 3, int d2Length = 3)
        : this(fastLength, slowLength, cycleLength, d1Length, d2Length, MovingAvgType.ExponentialMovingAverage) { }
    public SchaffTrendCycleShkSpecOptions(int fastLength, int slowLength, int cycleLength, int d1Length, int d2Length, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); CycleLength = Math.Max(1, cycleLength);
        D1Length = Math.Max(1, d1Length); D2Length = Math.Max(1, d2Length); MaType = maType;
    }
    public int FastLength { get; }
    public int SlowLength { get; }
    public int CycleLength { get; }
    public int D1Length { get; }
    public int D2Length { get; }
    public MovingAvgType MaType { get; }
}
