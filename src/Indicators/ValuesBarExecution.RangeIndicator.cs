#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRangeIndicatorState : IIndicatorState, IDisposable
    {
        private readonly RangeIndicatorWindow _range;
        private readonly ExponentialMovingAverageSmoother _average;
        public GeneratedRangeIndicatorState(int length) { _range = new(length); _average = new(length); }
        public double Update(in Bar bar) => _average.Next(_range.Next(bar.High, bar.Low, bar.Close, true), true);
        public void Reset() { _range.Reset(); _average.Reset(); }
        public void Dispose() { _range.Reset(); _average.Dispose(); }
    }
}
#endif
