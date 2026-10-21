#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedVolumePositiveNegativeState : IMultiOutputState, IDisposable
    {
        private readonly VolumePositiveNegativeWindow _window;
        public GeneratedVolumePositiveNegativeState(int length, int smooth) =>
            _window = new(MovingAvgType.ExponentialMovingAverage, length, smooth);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
