namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Trend periods and deviation distances for Kase Dev Stop V1.</summary>
public sealed class KaseDevStopV1SpecOptions : IIndicatorSpecOptions
{
    public KaseDevStopV1SpecOptions(int fastLength = 5, int slowLength = 21, int length = 20,
        double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
        : this(fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4, MovingAvgType.SimpleMovingAverage) { }
    public KaseDevStopV1SpecOptions(int fastLength, int slowLength, int length,
        double stdDev1, double stdDev2, double stdDev3, double stdDev4, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); Length = Math.Max(1, length);
        StdDev1 = stdDev1; StdDev2 = stdDev2; StdDev3 = stdDev3; StdDev4 = stdDev4; MaType = maType;
    }
    public int FastLength { get; }
    public int SlowLength { get; }
    public int Length { get; }
    public double StdDev1 { get; }
    public double StdDev2 { get; }
    public double StdDev3 { get; }
    public double StdDev4 { get; }
    public MovingAvgType MaType { get; }
}
