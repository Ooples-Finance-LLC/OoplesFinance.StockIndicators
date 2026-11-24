#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedKamaState(int length, int count) : IMultiOutputState, IDisposable
    {
        // Before the full period, the existing recurrence publishes the input
        // and zero efficiency. A batch-sized cap cannot leave that warmup.
        private readonly RoundedKaufmanWindow _window = new(Math.Max(1, Math.Min(length, count)));
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Efficiency; outputs[1] = point.Average;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
