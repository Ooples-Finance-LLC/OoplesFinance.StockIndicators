namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Oscillator and smoothing periods for the inverse Fisher transform.</summary>
public sealed class EhlersRelativeStrengthIndexInverseFisherTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersRelativeStrengthIndexInverseFisherTransformSpecOptions(int length = 14, int signalLength = 9)
        : this(length, signalLength, MovingAvgType.WeightedMovingAverage) { }
    public EhlersRelativeStrengthIndexInverseFisherTransformSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length); SignalLength = Math.Max(1, signalLength); MaType = maType;
    }
    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}
