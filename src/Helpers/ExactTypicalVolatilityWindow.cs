namespace OoplesFinance.StockIndicators.Helpers;

// Round each HLC mean once, then the full-window population deviation once.
internal sealed class ExactTypicalVolatilityWindow : IDisposable
{
    private readonly ExactPopulationWindow _window;
    internal ExactTypicalVolatilityWindow(int length) => _window = new ExactPopulationWindow(length);
    internal double Next(double high, double low, double close, bool commit)
    {
        var mean = new ExactMeanAccumulator();
        mean.Add(high); mean.Add(low); mean.Add(close);
        return _window.Next(mean.Mean(3), commit);
    }
    internal void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}
