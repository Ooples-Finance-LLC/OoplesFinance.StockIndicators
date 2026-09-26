namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Phase-median lookback and fixed-cycle gain for the adaptive cyber cycle.</summary>
public sealed class EhlersAdaptiveCyberCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveCyberCycleSpecOptions(int length = 5, double alpha = .07)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha) || alpha <= 0 || alpha >= 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be finite and between zero and one.");
        Length = Math.Max(1, length); Alpha = alpha;
    }
    public int Length { get; }
    public double Alpha { get; }
}
