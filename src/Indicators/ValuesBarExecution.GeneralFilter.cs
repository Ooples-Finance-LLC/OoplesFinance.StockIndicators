#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedGeneralFilterState(int length) : IIndicatorState, IDisposable
    {
        private readonly GeneralFilterWindow _window = new(length, 5.25, 1, 1);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Reset();
    }
}
#endif
