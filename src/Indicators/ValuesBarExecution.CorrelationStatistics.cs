#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedCorrelationStatisticsState : IIndicatorState, IDisposable
    {
        private readonly KendallCorrelationWindow? _kendall;
        private readonly ExactRSquaredWindow? _squared;
        internal GeneratedCorrelationStatisticsState(int length, int count, bool squared)
        {
            if (count == 0) return;
            if (!squared) _kendall = new(length, count);
            // R-squared is zero until a complete window is available.
            else if (Math.Max(1, length) <= count) _squared = new(length);
        }
        public double Update(in Bar bar) => _kendall?.Next(bar.Close, true) ?? _squared?.Next(bar.Close, true) ?? 0;
        public void Reset() { _kendall?.Reset(); _squared?.Reset(); }
        public void Dispose() { _kendall?.Dispose(); _squared?.Dispose(); }
    }
}
#endif
