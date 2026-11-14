#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedPivotDetectorState : IIndicatorState, IDisposable
    {
        // The public Length parameter is metadata; existing calculation routes use 200 and 14.
        private readonly PivotDetectorWindow _window = new(MovingAvgType.SimpleMovingAverage, 200, 14);
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
