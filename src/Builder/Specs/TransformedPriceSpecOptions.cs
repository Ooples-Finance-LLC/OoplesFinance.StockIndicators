using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>EMA periods and residual smoothing for the three wave outputs.</summary>
public sealed class EmaWaveIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EmaWaveIndicatorSpecOptions(int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3); SmoothLength = Math.Max(1, smoothLength);
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int SmoothLength { get; }
}

