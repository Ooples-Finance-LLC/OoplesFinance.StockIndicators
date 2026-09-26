namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Displaced mean period and percentage widths for Hurst bands.</summary>
public sealed class HurstBandsSpecOptions : IIndicatorSpecOptions
{
    public HurstBandsSpecOptions(int length = 10, double innerMult = 1.6, double outerMult = 2.6, double extremeMult = 4.2)
    { Length = Math.Max(1, length); InnerMult = innerMult; OuterMult = outerMult; ExtremeMult = extremeMult; }
    public int Length { get; }
    public double InnerMult { get; }
    public double OuterMult { get; }
    public double ExtremeMult { get; }
}
