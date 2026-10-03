namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Period and bandwidth for the nine Ehlers Swiss Army Knife filters.</summary>
public sealed class EhlersSwissArmyKnifeSpecOptions : IIndicatorSpecOptions
{
    public EhlersSwissArmyKnifeSpecOptions(int length = 20, double delta = .1)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        Length = Math.Max(1, length); Delta = delta;
    }
    public int Length { get; }
    public double Delta { get; }
}
