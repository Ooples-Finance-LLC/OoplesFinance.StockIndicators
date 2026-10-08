namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Roofing, cycle-scan, and adaptive bandpass parameters.</summary>
public sealed class EhlersAdaptiveBandPassFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveBandPassFilterSpecOptions(int length1 = 48, int length2 = 10, int length3 = 3, double bw = .3)
    {
        if (double.IsNaN(bw) || double.IsInfinity(bw) || bw < 0) throw new ArgumentOutOfRangeException(nameof(bw));
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3); Bw = bw;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public double Bw { get; }
}
