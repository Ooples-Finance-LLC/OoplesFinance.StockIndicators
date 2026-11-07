#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedThreeHullState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ThreeHullWindow _window = new(MovingAvgType.WeightedMovingAverage, length, count);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
