namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Trend lag, forecast horizon, and forecast-error band multiplier.</summary>
public sealed class GrandTrendForecastingSpecOptions : IIndicatorSpecOptions
{
    public GrandTrendForecastingSpecOptions(int length = 100, int forecastLength = 200, double mult = 2)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult) || mult < 0) throw new ArgumentOutOfRangeException(nameof(mult));
        Length = Math.Max(1, length); ForecastLength = Math.Max(1, forecastLength); Mult = mult;
    }
    public int Length { get; }
    public int ForecastLength { get; }
    public double Mult { get; }
}
