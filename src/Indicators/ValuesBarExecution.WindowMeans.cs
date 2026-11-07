#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedWindowMeansState : IIndicatorState, IDisposable
    {
        private readonly NaturalWindowMean? _natural;
        private readonly FareyWindowMean? _farey;
        private readonly OptimalWeightedWindow? _optimal;
        internal GeneratedWindowMeansState(int length, int count, int kind)
        {
            if (count == 0) return;
            if (kind == 0) _natural = new(Math.Clamp(length, 1, count));
            else if (kind == 1) _farey = new(length);
            else _optimal = new(length);
        }
        public double Update(in Bar bar) => _natural?.Next(bar.Close, true)
            ?? _farey?.Next(bar.Close, true) ?? _optimal?.Next(bar.Close, true) ?? 0;
        public void Reset() { _natural?.Reset(); _farey?.Reset(); _optimal?.Reset(); }
        public void Dispose() { _natural?.Dispose(); _farey?.Dispose(); _optimal?.Dispose(); }
    }
}
#endif
