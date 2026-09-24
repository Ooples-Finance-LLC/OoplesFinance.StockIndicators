namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class UltimateMomentumIndicatorSpecOptions : IIndicatorSpecOptions
{
    public UltimateMomentumIndicatorSpecOptions(int length1 = 13, int length2 = 19, int length3 = 21,
        int length4 = 39, int length5 = 50, double stdDevMult = 1.5)
        : this(length1, length2, length3, length4, length5, stdDevMult, MovingAvgType.SimpleMovingAverage) { }
    public UltimateMomentumIndicatorSpecOptions(int length1, int length2, int length3,
        int length4, int length5, double stdDevMult, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4); Length5 = Math.Max(1, length5); StdDevMult = stdDevMult; MaType = maType;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}
