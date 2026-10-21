#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSwenlinState : IMultiOutputState, IDisposable
    {
        private readonly ExponentialMovingAverageSmoother _first, _second, _signal;
        private double _previous;
        public GeneratedSwenlinState(int length)
        {
            _first = new(length); _second = new(length); _signal = new(length);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var vote = bar.Close > _previous ? 1000d : bar.Close < _previous ? -1000d : 0d;
            var line = _second.Next(_first.Next(vote, true), true);
            outputs[0] = line; outputs[1] = _signal.Next(line, true); _previous = bar.Close;
        }
        public void Reset() { _first.Reset(); _second.Reset(); _signal.Reset(); _previous = 0; }
        public void Dispose() { _first.Dispose(); _second.Dispose(); _signal.Dispose(); }
    }
}
#endif
