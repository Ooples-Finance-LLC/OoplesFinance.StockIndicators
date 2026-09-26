namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class SineMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly SineWindowMean _mean;
    internal SineMovingAverageSmoother(int length) => _mean = new SineWindowMean(length);
    public double Next(double value, bool commit) => _mean.Next(value, commit);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class NaturalMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly NaturalWindowMean _mean;
    internal NaturalMovingAverageSmoother(int length) => _mean = new NaturalWindowMean(length);
    public double Next(double value, bool commit) => _mean.Next(value, commit);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}
