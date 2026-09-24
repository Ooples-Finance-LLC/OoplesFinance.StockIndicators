namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class EhlersSineWaveIndicatorV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersSineWaveIndicatorV1SpecOptions() { }
}

public sealed class EhlersSineWaveIndicatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersSineWaveIndicatorV2SpecOptions(int length = 5, double alpha = .07)
    { Length = Math.Max(1, length); Alpha = alpha; }
    public int Length { get; }
    public double Alpha { get; }
}
