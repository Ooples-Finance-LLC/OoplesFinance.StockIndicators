#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRangeStatisticsState : IIndicatorState, IDisposable
    {
        private readonly RollingWindowMax? _maximum;
        private readonly RollingWindowMin? _minimum;
        private readonly int _kind;
        internal GeneratedRangeStatisticsState(int length, int count, int kind)
        {
            // No observation expires when the requested period covers the batch.
            length = Math.Clamp(length, 1, Math.Max(1, count));
            _kind = kind;
            _maximum = new(length);
            _minimum = new(length);
        }
        public double Update(in Bar bar)
        {
            var maximum = _maximum?.Add(_kind == 3 ? bar.High : bar.Close, out _) ?? 0;
            var minimum = _minimum?.Add(_kind == 3 ? bar.Low : bar.Close, out _) ?? 0;
            return PriceMean.Of(maximum, minimum);
        }
        public void Reset() { _maximum?.Reset(); _minimum?.Reset(); }
        public void Dispose() { _maximum?.Dispose(); _minimum?.Dispose(); }
    }
}
#endif
