namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Cycle fraction for the MAMA-period adaptive oscillator.</summary>
public sealed class EhlersAdaptiveCommodityChannelIndexV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveCommodityChannelIndexV1SpecOptions(double cycPart = 1, double constant = .015)
    {
        if (double.IsNaN(cycPart) || double.IsInfinity(cycPart) || cycPart <= 0) throw new ArgumentOutOfRangeException(nameof(cycPart));
        CycPart = cycPart;
        if (double.IsNaN(constant) || double.IsInfinity(constant) || constant <= 0) throw new ArgumentOutOfRangeException(nameof(constant));
        Constant = constant;
    }
    public double CycPart { get; }
    public double Constant { get; }
}
