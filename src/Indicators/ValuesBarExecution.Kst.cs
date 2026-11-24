#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedKstState(int[] lookbacks, int[] smoothing, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly RocBankWindow _window = new(MovingAvgType.SimpleMovingAverage, lookbacks, smoothing, [1, 2, 3, 4], signal, count);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
