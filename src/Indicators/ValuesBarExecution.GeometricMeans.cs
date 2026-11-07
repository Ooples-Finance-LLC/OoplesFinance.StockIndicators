#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedGeometricMeansState : IIndicatorState, IDisposable
    {
        private readonly RollingGeometricMean? _mean;
        private readonly bool _positiveOnly;
        internal GeneratedGeometricMeansState(int length, int count, bool positiveOnly)
        {
            _positiveOnly = positiveOnly;
            // A window larger than this batch cannot leave its warmup path.
            if (Math.Max(1, length) <= count) _mean = new(length, positiveOnly);
        }
        public double Update(in Bar bar) => _mean?.Next(bar.Close, true) ?? (_positiveOnly ? bar.Close : 0);
        public void Reset() => _mean?.Reset();
        public void Dispose() => _mean?.Dispose();
    }
}
#endif
