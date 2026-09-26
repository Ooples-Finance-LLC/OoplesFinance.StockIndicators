namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Cycle periods and signal average for the adaptive stochastic inverse Fisher transform.</summary>
public sealed class EhlersAdaptiveStochasticInverseFisherTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveStochasticInverseFisherTransformSpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage) { }
    public EhlersAdaptiveStochasticInverseFisherTransformSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3); MaType = maType; }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}
