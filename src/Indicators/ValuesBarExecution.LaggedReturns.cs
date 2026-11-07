#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedLaggedReturnState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly bool _logarithmic;
        private readonly PooledRingBuffer<double>? _prices;
        internal GeneratedLaggedReturnState(int length, int count, bool logarithmic)
        {
            _length = Math.Max(1, length);
            _logarithmic = logarithmic;
            // No bar can reach its lag when the period covers the entire batch.
            if (_length < count) _prices = new(_length);
        }
        public double Update(in Bar bar)
        {
            if (_prices is null) return 0;
            var result = _prices.Count < _length ? 0 : _logarithmic
                ? StableLogRatio.Of(bar.Close, _prices[0])
                : RoundedRangeRatio.Of(bar.Close, _prices[0], _prices[0]);
            _prices.TryAdd(bar.Close, out _);
            return result;
        }
        public void Reset() => _prices?.Clear();
        public void Dispose() => _prices?.Dispose();
    }
}
#endif
