namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Options for the ratio of gains above a target to losses below it.</summary>
public sealed class OmegaRatioSpecOptions : IIndicatorSpecOptions
{
    public OmegaRatioSpecOptions(int length = 30, double bmk = .05) { Length = Math.Max(1, length); Bmk = bmk; }
    public int Length { get; }
    public double Bmk { get; }
}

/// <summary>Options for upside potential divided by downside deviation.</summary>
public sealed class UpsidePotentialRatioSpecOptions : IIndicatorSpecOptions
{
    public UpsidePotentialRatioSpecOptions(int length = 30, double bmk = .05) { Length = Math.Max(1, length); Bmk = bmk; }
    public int Length { get; }
    public double Bmk { get; }
}

/// <summary>Options for mean excess returns divided by supplied beta.</summary>
public sealed class TreynorRatioSpecOptions : IIndicatorSpecOptions
{
    public TreynorRatioSpecOptions(int length = 30, double beta = 1, double bmk = .02)
    { Length = Math.Max(1, length); Beta = beta; Bmk = bmk; }
    public int Length { get; }
    public double Beta { get; }
    public double Bmk { get; }
}
