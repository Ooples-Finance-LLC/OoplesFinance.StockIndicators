#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRunningEquityState(int length) : IIndicatorState, IDisposable
    {
        private readonly RunningEquityWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
}
#endif
