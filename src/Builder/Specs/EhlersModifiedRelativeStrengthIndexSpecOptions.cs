namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Roofing, smoothing, and common gain/loss window for modified RSI.</summary>
public sealed class EhlersModifiedRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public EhlersModifiedRelativeStrengthIndexSpecOptions(int length1 = 48, int length2 = 10, int length3 = 10)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3); }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
}
