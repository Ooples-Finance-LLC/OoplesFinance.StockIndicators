namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Options for a logistic transform of rolling price/time correlation.</summary>
public sealed class LogisticCorrelationSpecOptions : IIndicatorSpecOptions
{
    public LogisticCorrelationSpecOptions(int length = 100, double k = 10)
    {
        Length = Math.Max(1, length);
        K = k;
    }
    public int Length { get; }
    public double K { get; }
}
