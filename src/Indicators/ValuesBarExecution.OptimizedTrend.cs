#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedOptimizedTrendState : IIndicatorState, IDisposable
    {
        private readonly OptimizedTrendWindow _window;
        public GeneratedOptimizedTrendState(int length, double percent) =>
            _window = new(MovingAvgType.VariableIndexDynamicAverage, length, percent);
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
