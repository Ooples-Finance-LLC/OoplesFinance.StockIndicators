#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedNaturalLiquidState(int length) : IIndicatorState, IDisposable
    {
        private readonly LiquidRsiWindow _window = new(length);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Reset() => _window.Reset();
        public void Dispose() => Reset();
    }
}
#endif
