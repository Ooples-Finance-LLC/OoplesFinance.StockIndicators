namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Options for cumulative efficiency-weighted price changes.</summary>
public sealed class EfficientPriceSpecOptions : IIndicatorSpecOptions
{
    public EfficientPriceSpecOptions(int length = 50) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Options for the efficiency-dependent price deadband.</summary>
public sealed class EfficientAutoLineSpecOptions : IIndicatorSpecOptions
{
    public EfficientAutoLineSpecOptions(int length = 19, double fastAlpha = .0001, double slowAlpha = .005)
    {
        Length = Math.Max(1, length);
        FastAlpha = fastAlpha;
        SlowAlpha = slowAlpha;
    }
    public int Length { get; }
    public double FastAlpha { get; }
    public double SlowAlpha { get; }
}
