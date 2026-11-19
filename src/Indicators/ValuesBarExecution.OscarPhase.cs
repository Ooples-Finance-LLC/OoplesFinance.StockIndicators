#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedOscarState(int length) : IIndicatorState, IDisposable
    {
        private readonly OscarWindow _window = new(length);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedPhaseChangeState(int length, int smooth) : IMultiOutputState, IDisposable
    {
        private readonly PhaseChangeWindow _window = new(MovingAvgType.SimpleMovingAverage, length, smooth);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
