namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class AlmaMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly AlmaWindowMean _mean;
    internal AlmaMovingAverageSmoother(int length) => _mean = new AlmaWindowMean(length, .85, 6);
    public double Next(double value, bool commit) => _mean.Next(value, commit);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}
