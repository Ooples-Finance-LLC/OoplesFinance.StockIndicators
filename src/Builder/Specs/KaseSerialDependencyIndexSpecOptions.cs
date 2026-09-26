namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Log-return volatility window and price lookback for Kase serial dependency.</summary>
public sealed class KaseSerialDependencyIndexSpecOptions : IIndicatorSpecOptions
{
    public KaseSerialDependencyIndexSpecOptions(int length = 14) => Length = Math.Max(1, length);
    public int Length { get; }
}
