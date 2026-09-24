namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Price range endpoints for Swami stochastic smoothing.</summary>
public sealed class SwamiStochasticsSpecOptions : IIndicatorSpecOptions
{
    public SwamiStochasticsSpecOptions(int fastLength = 12, int slowLength = 48)
    { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); }
    public int FastLength { get; }
    public int SlowLength { get; }
}
