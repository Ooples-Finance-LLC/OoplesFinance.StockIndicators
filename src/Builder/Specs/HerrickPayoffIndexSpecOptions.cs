namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Point value for the volume-weighted median-price payoff.</summary>
public sealed class HerrickPayoffIndexSpecOptions : IIndicatorSpecOptions
{
    public HerrickPayoffIndexSpecOptions(double pointValue = 100)
    {
        if (double.IsNaN(pointValue) || double.IsInfinity(pointValue)) throw new ArgumentOutOfRangeException(nameof(pointValue));
        PointValue = pointValue;
    }
    public double PointValue { get; }
}
