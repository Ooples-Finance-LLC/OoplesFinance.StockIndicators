namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Bandpass period and bandwidth for the zero-crossing cycle estimate.</summary>
public sealed class EhlersZeroCrossingsDominantCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersZeroCrossingsDominantCycleSpecOptions(int length = 20, double bw = .7)
    {
        if (double.IsNaN(bw) || double.IsInfinity(bw) || bw < 0) throw new ArgumentOutOfRangeException(nameof(bw));
        Length = Math.Max(1, length); Bw = bw;
    }
    public int Length { get; }
    public double Bw { get; }
}
