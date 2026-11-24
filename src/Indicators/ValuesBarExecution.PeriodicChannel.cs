#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedPeriodicChannelState(int period, int lookback) : IMultiOutputState, IDisposable
    {
        private readonly PeriodicChannelWindow _window = new(period, lookback);
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.Close, true).Values.AsSpan().CopyTo(outputs);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
