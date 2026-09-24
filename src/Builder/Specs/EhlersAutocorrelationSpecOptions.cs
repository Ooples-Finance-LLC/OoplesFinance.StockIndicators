namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Roofing and correlation periods for the Ehlers autocorrelation indicator.</summary>
public sealed class EhlersAutoCorrelationIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersAutoCorrelationIndicatorSpecOptions(int length1 = 48, int length2 = 10)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>Periods and first Fourier lag for the autocorrelation periodogram.</summary>
public sealed class EhlersAutoCorrelationPeriodogramSpecOptions : IIndicatorSpecOptions
{
    public EhlersAutoCorrelationPeriodogramSpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(0, length3); }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
}
