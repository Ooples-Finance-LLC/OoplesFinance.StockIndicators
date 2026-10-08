namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Sine period and trend-correlation lookback for the periodic channel.</summary>
public sealed class PeriodicChannelSpecOptions : IIndicatorSpecOptions
{
    public PeriodicChannelSpecOptions(int length1 = 500, int length2 = 2)
    { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; }
    public int Length2 { get; }
}
