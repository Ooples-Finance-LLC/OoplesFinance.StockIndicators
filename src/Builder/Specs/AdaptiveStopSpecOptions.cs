namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Breakout lookback and distance in percentage points (10 means ten percent).</summary>
public sealed class PercentageTrailingStopsSpecOptions : IIndicatorSpecOptions
{
    public PercentageTrailingStopsSpecOptions(int length = 100, double pct = 10)
    { Length = Math.Max(1, length); Pct = pct; }
    public int Length { get; }
    public double Pct { get; }
}

/// <summary>Efficiency lookback and exponent for adaptive moment weights.</summary>
public sealed class KaufmanAdaptiveBandsSpecOptions : IIndicatorSpecOptions
{
    public KaufmanAdaptiveBandsSpecOptions(int length = 100, double stdDevFactor = 3)
    { Length = Math.Max(1, length); StdDevFactor = stdDevFactor; }
    public int Length { get; }
    public double StdDevFactor { get; }
}

/// <summary>Efficiency lookback and fast/slow dispersion windows for step channels.</summary>
public sealed class EfficientTrendStepChannelSpecOptions : IIndicatorSpecOptions
{
    public EfficientTrendStepChannelSpecOptions(int length = 100, int fastLength = 50, int slowLength = 200)
    { Length = Math.Max(1, length); FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); }
    public int Length { get; }
    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>McNicholl envelope period and deviation multiplier.</summary>
public sealed class DEnvelopeSpecOptions : IIndicatorSpecOptions
{
    public DEnvelopeSpecOptions(int length = 20, double devFactor = 2)
    { Length = Math.Max(1, length); DevFactor = devFactor; }
    public int Length { get; }
    public double DevFactor { get; }
}

/// <summary>Percentage distance for the Nick Rypock reversal threshold.</summary>
public sealed class NickRypockTrailingReverseSpecOptions : IIndicatorSpecOptions
{
    public NickRypockTrailingReverseSpecOptions(int length = 2) => Length = Math.Max(1, length);
    public int Length { get; }
}
