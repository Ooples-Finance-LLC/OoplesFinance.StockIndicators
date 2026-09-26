namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class EhlersFourierSeriesAnalysisSpecOptions : IIndicatorSpecOptions
{
    public EhlersFourierSeriesAnalysisSpecOptions(int length = 20, double bw = .1)
    { Length = Math.Max(1, length); Bw = bw; }
    public int Length { get; }
    public double Bw { get; }
}
