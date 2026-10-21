#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedKalmanSmootherState(int length) : IIndicatorState
    {
        private readonly KalmanSmootherWindow _window = new(length);
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Reset() => _window.Reset();
    }
}
#endif
