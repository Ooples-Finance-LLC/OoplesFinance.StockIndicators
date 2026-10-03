namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Adaptive change-deviation filter settings.</summary>
public sealed class MovingAverageAdaptiveFilterSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageAdaptiveFilterSpecOptions(int length = 10, double filter = .15, double fastAlpha = .667, double slowAlpha = .0645)
    {
        if (double.IsNaN(filter) || double.IsInfinity(filter) || filter < 0) throw new ArgumentOutOfRangeException(nameof(filter));
        if (double.IsNaN(fastAlpha) || fastAlpha < 0 || fastAlpha > 1) throw new ArgumentOutOfRangeException(nameof(fastAlpha));
        if (double.IsNaN(slowAlpha) || slowAlpha < 0 || slowAlpha > 1) throw new ArgumentOutOfRangeException(nameof(slowAlpha));
        Length = Math.Max(1, length); Filter = filter; FastAlpha = fastAlpha; SlowAlpha = slowAlpha;
    }
    public int Length { get; }
    public double Filter { get; }
    public double FastAlpha { get; }
    public double SlowAlpha { get; }
}
