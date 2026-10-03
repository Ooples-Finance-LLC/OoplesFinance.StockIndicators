namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Oscillator and smoothing periods for the inverse Fisher transform.</summary>
public sealed class EhlersCommodityChannelIndexInverseFisherTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersCommodityChannelIndexInverseFisherTransformSpecOptions(int length = 20, int signalLength = 9, double constant = .015)
        : this(length, signalLength, constant, MovingAvgType.WeightedMovingAverage) { }
    public EhlersCommodityChannelIndexInverseFisherTransformSpecOptions(int length, int signalLength, double constant, MovingAvgType maType)
    {
        Length = Math.Max(1, length); SignalLength = Math.Max(1, signalLength); MaType = maType;
        if (!(constant > 0) || double.IsInfinity(constant)) throw new ArgumentOutOfRangeException(nameof(constant));
        Constant = constant;
    }
    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
    public double Constant { get; }
}
