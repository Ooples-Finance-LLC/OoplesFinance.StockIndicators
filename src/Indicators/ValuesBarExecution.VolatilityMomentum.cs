#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedVolatilityMomentumState : IMultiOutputState, IDisposable
    {
        private readonly VolatilityMomentumWindow _window;
        public GeneratedVolatilityMomentumState(int length, int smooth) =>
            _window = new(MovingAvgType.WildersSmoothingMethod, length, smooth);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
