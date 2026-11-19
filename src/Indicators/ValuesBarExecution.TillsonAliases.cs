#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedTillsonAliasState(int length, double factor) : IIndicatorState, IDisposable
    {
        private readonly TillsonWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, factor);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
