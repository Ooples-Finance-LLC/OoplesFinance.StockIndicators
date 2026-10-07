namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Deviation smoothing period and previous-filter weight for Super Trend Filter.</summary>
public sealed class SuperTrendFilterSpecOptions : IIndicatorSpecOptions
{
    public SuperTrendFilterSpecOptions(int length = 200, double factor = .9)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < 0 || factor > 1)
            throw new ArgumentOutOfRangeException(nameof(factor), "Factor must be finite and between zero and one.");
        Length = Math.Max(1, length); Factor = factor;
    }
    public int Length { get; }
    public double Factor { get; }
}
