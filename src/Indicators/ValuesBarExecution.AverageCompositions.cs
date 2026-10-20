#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedAverageCompositionsState : IIndicatorState, IDisposable
    {
        private readonly WmaValueState? _weighted;
        private readonly RoundedSimpleMovingAverageSmoother? _simple;
        private readonly McGinleyWindow? _dynamic;
        private readonly bool _leo;
        internal GeneratedAverageCompositionsState(int length, int count, int kind)
        {
            length = Math.Max(1, length);
            _leo = kind == 1;
            if (kind == 2) _dynamic = new(length, .6);
            else
            {
                _weighted = new(length, count);
                // The simple component remains zero until its full window exists.
                if (_leo && length <= count) _simple = new(length);
            }
        }
        public double Update(in Bar bar)
        {
            if (_dynamic is not null) return _dynamic.Next(bar.Close, true);
            var weighted = _weighted!.Update(in bar);
            return _leo ? LeoAverage.Combine(weighted, _simple?.Next(bar.Close, true) ?? 0) : weighted;
        }
        public void Reset() { _weighted?.Reset(); _simple?.Reset(); _dynamic?.Reset(); }
        public void Dispose() => _simple?.Dispose();
    }
}
#endif
