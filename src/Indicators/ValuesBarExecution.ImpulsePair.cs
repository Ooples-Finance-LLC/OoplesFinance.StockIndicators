#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedImpulsePairState : IMultiOutputState, IDisposable
    {
        private readonly ExponentialMovingAverageSmoother _first, _second;
        private readonly WilderMovingAverageSmoother _high, _low;
        private readonly RoundedPartialMeanSmoother _signal;
        private readonly bool _percentage;
        public GeneratedImpulsePairState(int length, int signal, int count, bool percentage)
        {
            _first = new(length); _second = new(length); _high = new(length); _low = new(length);
            // The signal averages the available prefix; periods beyond the batch never evict.
            _signal = new(Math.Max(1, Math.Min(signal, count))); _percentage = percentage;
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var typical = RollingMoneyFlowIndex.TypicalPrice(bar.High, bar.Low, bar.Close);
            var first = _first.Next(typical, true); var second = _second.Next(first, true);
            var middle = ExponentialExtrapolation.Double(first, second);
            var line = RoundedImpulseOscillator.Line(middle, _high.Next(bar.High, true), _low.Next(bar.Low, true), _percentage);
            var signal = _signal.Next(line, true);
            outputs[0] = line; outputs[1] = signal; outputs[2] = line - signal;
        }
        public void Reset() { _first.Reset(); _second.Reset(); _high.Reset(); _low.Reset(); _signal.Reset(); }
        public void Dispose() { _first.Dispose(); _second.Dispose(); _high.Dispose(); _low.Dispose(); _signal.Dispose(); }
    }
}
#endif
