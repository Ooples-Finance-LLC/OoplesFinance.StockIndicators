namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Efficiency period, adaptive gains, and deviation threshold for Kaufman Binary Wave.</summary>
public sealed class KaufmanBinaryWaveSpecOptions : IIndicatorSpecOptions
{
    public KaufmanBinaryWaveSpecOptions(int length = 20, double fastSc = .6022, double slowSc = .0645, double filterPct = 10)
    {
        if (double.IsNaN(fastSc) || double.IsInfinity(fastSc) || fastSc < 0) throw new ArgumentOutOfRangeException(nameof(fastSc));
        if (double.IsNaN(slowSc) || double.IsInfinity(slowSc) || slowSc < 0) throw new ArgumentOutOfRangeException(nameof(slowSc));
        if (double.IsNaN(filterPct) || double.IsInfinity(filterPct) || filterPct < 0) throw new ArgumentOutOfRangeException(nameof(filterPct));
        Length = Math.Max(1, length); FastSc = fastSc; SlowSc = slowSc; FilterPct = filterPct;
    }
    public int Length { get; }
    public double FastSc { get; }
    public double SlowSc { get; }
    public double FilterPct { get; }
}
