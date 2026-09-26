namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Periods and coefficients for EhlersHilbertTransformIndicator.</summary>
public sealed class EhlersHilbertTransformIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersHilbertTransformIndicatorSpecOptions(int length = 7, double iMult = .635, double qMult = .338)
    { Length = Math.Max(1, length); IMult = iMult; QMult = qMult; }
    public int Length { get; } public double IMult { get; } public double QMult { get; }
}

/// <summary>Periods and coefficients for EhlersHilbertTransformer.</summary>
public sealed class EhlersHilbertTransformerSpecOptions : IIndicatorSpecOptions
{
    public EhlersHilbertTransformerSpecOptions(int length1 = 48, int length2 = 20)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; } public int Length2 { get; }
}

/// <summary>Periods and coefficients for EhlersHilbertTransformerIndicator.</summary>
public sealed class EhlersHilbertTransformerIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersHilbertTransformerIndicatorSpecOptions(int length1 = 48, int length2 = 20, int length3 = 10)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3); }
    public int Length1 { get; } public int Length2 { get; } public int Length3 { get; }
}

/// <summary>Periods and coefficients for EhlersInstantaneousPhaseIndicator.</summary>
public sealed class EhlersInstantaneousPhaseIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersInstantaneousPhaseIndicatorSpecOptions(int length1 = 7, int length2 = 50)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; } public int Length2 { get; }
}

