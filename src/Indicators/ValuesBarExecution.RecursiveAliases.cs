#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRecursiveAliasState(int length) : IIndicatorState, IDisposable
    {
        private readonly JmaWindow _window = new(length, 50, 2);
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Reset() => _window.Reset();
        public void Dispose() => Reset();
    }
}
#endif
