#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedMiddleMeanState : IIndicatorState, IDisposable
    {
        private readonly RollingWindowMax _high;
        private readonly RollingWindowMin _low;
        private readonly ExponentialMovingAverageSmoother _average;
        public GeneratedMiddleMeanState(int smoothing, int window, int count)
        {
            var capacity = Math.Max(1, Math.Min(window, count));
            _high = new(capacity); _low = new(capacity); _average = new(smoothing);
        }
        public double Update(in Bar bar)
        {
            var high = _high.Add(bar.Close, out _); var low = _low.Add(bar.Close, out _);
            return _average.Next(PriceMean.Of(high, low), true);
        }
        public void Reset() { _high.Reset(); _low.Reset(); _average.Reset(); }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _average.Dispose(); }
    }
}
#endif
