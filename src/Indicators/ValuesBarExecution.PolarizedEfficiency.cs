#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedPolarizedEfficiencyState(int length, int smooth) : IIndicatorState, IDisposable
    {
        private readonly PolarizedEfficiencyWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smooth);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
