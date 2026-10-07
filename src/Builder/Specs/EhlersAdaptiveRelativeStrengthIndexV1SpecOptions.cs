namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Cycle fraction for the MAMA-period adaptive oscillator.</summary>
public sealed class EhlersAdaptiveRelativeStrengthIndexV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveRelativeStrengthIndexV1SpecOptions(double cycPart = .5)
    {
        if (double.IsNaN(cycPart) || double.IsInfinity(cycPart) || cycPart <= 0) throw new ArgumentOutOfRangeException(nameof(cycPart));
        CycPart = cycPart;
    }
    public double CycPart { get; }
}
