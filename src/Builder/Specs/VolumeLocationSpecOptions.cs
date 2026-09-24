namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Rolling volume-weighted signed close location.</summary>
public sealed class VolumeAccumulationPercentSpecOptions : IIndicatorSpecOptions
{
    public VolumeAccumulationPercentSpecOptions(int length = 10) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Previous-bar midpoint offsets and volume classification parameters.</summary>
public sealed class HawkeyeVolumeIndicatorSpecOptions : IIndicatorSpecOptions
{
    public HawkeyeVolumeIndicatorSpecOptions(int length = 200, double divisor = 3.6)
    { Length = Math.Max(1, length); Divisor = divisor; }
    public int Length { get; }
    public double Divisor { get; }
}

/// <summary>Lookbacks used by Better Volume's classification signals.</summary>
public sealed class BetterVolumeIndicatorSpecOptions : IIndicatorSpecOptions
{
    public BetterVolumeIndicatorSpecOptions(int length = 8, int lbLength = 2)
    { Length = Math.Max(1, length); LbLength = Math.Max(1, lbLength); }
    public int Length { get; }
    public int LbLength { get; }
}

/// <summary>Current high averaged with the low two bars earlier.</summary>
public sealed class EarningSupportResistanceLevelsSpecOptions : IIndicatorSpecOptions { }

/// <summary>Band lookback and signal thresholds for on-balance volume disparity.</summary>
public sealed class OnBalanceVolumeDisparityIndicatorSpecOptions : IIndicatorSpecOptions
{
    public OnBalanceVolumeDisparityIndicatorSpecOptions(int length = 33, int signalLength = 4, double top = 1.1, double bottom = .9,
        OoplesFinance.StockIndicators.Enums.MovingAvgType maType = OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage)
    { Length = Math.Max(1, length); SignalLength = Math.Max(1, signalLength); Top = top; Bottom = bottom; MaType = maType; }
    public int Length { get; } public int SignalLength { get; } public double Top { get; } public double Bottom { get; }
    public OoplesFinance.StockIndicators.Enums.MovingAvgType MaType { get; }
}
