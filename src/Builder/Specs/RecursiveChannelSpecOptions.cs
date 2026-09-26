namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Contraction period; at least two preserves nonnegative channel width.</summary>
public sealed class GChannelsSpecOptions : IIndicatorSpecOptions
{
    public GChannelsSpecOptions(int length = 100) => Length = Math.Max(2, length);
    public int Length { get; }
}

/// <summary>Envelope response period and signed expansion/contraction factor.</summary>
public sealed class SmartEnvelopeSpecOptions : IIndicatorSpecOptions
{
    public SmartEnvelopeSpecOptions(int length = 14, double factor = 1)
    { Length = Math.Max(1, length); Factor = factor; }
    public int Length { get; }
    public double Factor { get; }
}

/// <summary>Window defining the two-standard-deviation threshold for a new step.</summary>
public sealed class TrendStepSpecOptions : IIndicatorSpecOptions
{
    public TrendStepSpecOptions(int length = 50) => Length = Math.Max(1, length);
    public int Length { get; }
}
