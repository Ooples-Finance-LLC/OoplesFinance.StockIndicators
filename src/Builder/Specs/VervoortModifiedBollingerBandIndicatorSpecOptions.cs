namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Heikin-Ashi band-position and outer deviation periods.</summary>
public sealed class VervoortModifiedBollingerBandIndicatorSpecOptions : IIndicatorSpecOptions
{
    public VervoortModifiedBollingerBandIndicatorSpecOptions(int length1 = 18, int length2 = 200,
        int smoothLength = 8, double stdDevMult = 1.6)
        : this(length1, length2, smoothLength, stdDevMult, MovingAvgType.TripleExponentialMovingAverage) { }
    public VervoortModifiedBollingerBandIndicatorSpecOptions(int length1, int length2,
        int smoothLength, double stdDevMult, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2);
        SmoothLength = Math.Max(1, smoothLength); StdDevMult = stdDevMult; MaType = maType;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothLength { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}
