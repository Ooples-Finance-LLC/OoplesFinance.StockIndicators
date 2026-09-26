namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Comb period range and bandpass bandwidth.</summary>
public sealed class EhlersCombFilterSpectralEstimateSpecOptions : IIndicatorSpecOptions
{
    public EhlersCombFilterSpectralEstimateSpecOptions(int length1 = 48, int length2 = 10, double bw = .3)
    {
        if (double.IsNaN(bw) || double.IsInfinity(bw) || bw < 0) throw new ArgumentOutOfRangeException(nameof(bw));
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Bw = bw;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public double Bw { get; }
}
