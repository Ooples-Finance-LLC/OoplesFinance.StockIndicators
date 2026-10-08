namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>High-pass, low-pass, and lag-one correlation window periods.</summary>
public sealed class EhlersConvolutionIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersConvolutionIndicatorSpecOptions(int length1 = 80, int length2 = 40, int length3 = 48)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3); }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
}
