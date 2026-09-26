namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Periods and extrapolation multiplier for LinearChannels.</summary>
public sealed class LinearChannelsSpecOptions : IIndicatorSpecOptions
{
    public LinearChannelsSpecOptions(int length = 14, double mult = 50)
    { Length = Math.Max(1, length); Mult = mult; }
    public int Length { get; }
    public double Mult { get; }
}

/// <summary>Periods and extrapolation multiplier for LinearTrailingStop.</summary>
public sealed class LinearTrailingStopSpecOptions : IIndicatorSpecOptions
{
    public LinearTrailingStopSpecOptions(int length = 14, double mult = 28)
    { Length = Math.Max(1, length); Mult = mult; }
    public int Length { get; }
    public double Mult { get; }
}

/// <summary>Periods for MotionToAttractionChannels.</summary>
public sealed class MotionToAttractionChannelsSpecOptions : IIndicatorSpecOptions
{
    public MotionToAttractionChannelsSpecOptions(int length = 14)
    { Length = Math.Max(1, length);  }
    public int Length { get; }

}

/// <summary>Periods for MotionToAttractionTrailingStop.</summary>
public sealed class MotionToAttractionTrailingStopSpecOptions : IIndicatorSpecOptions
{
    public MotionToAttractionTrailingStopSpecOptions(int length = 14)
    { Length = Math.Max(1, length);  }
    public int Length { get; }

}

