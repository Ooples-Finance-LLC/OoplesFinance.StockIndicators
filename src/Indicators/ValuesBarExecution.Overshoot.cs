#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedOvershootState : IIndicatorState, IDisposable
    {
        private readonly OvershootWindow? _window;
        public GeneratedOvershootState(int length, int count)
        {
            // Before the full window, the default SMA and published fit are zero.
            if (Math.Max(1, length) <= count) _window = new(MovingAvgType.SimpleMovingAverage, length);
        }
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Reset() => _window?.Reset();
        public void Dispose() => _window?.Dispose();
    }
}
#endif
