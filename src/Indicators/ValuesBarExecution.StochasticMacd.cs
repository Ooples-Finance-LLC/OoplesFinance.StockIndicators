#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedStochasticMacdState : IMultiOutputState, IDisposable
    {
        private readonly RollingWindowMax _high;
        private readonly RollingWindowMin _low;
        private readonly ExponentialMovingAverageSmoother _fast = new(12), _slow = new(26), _signal = new(9);
        private bool _signalInvalid;
        public GeneratedStochasticMacdState(int length, int count)
        {
            var capacity = Math.Max(1, Math.Min(length, count));
            _high = new(capacity); _low = new(capacity);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var line = RoundedStochasticMacd.Of(_fast.Next(bar.Close, true), _slow.Next(bar.Close, true), _high.Add(bar.High, out _), _low.Add(bar.Low, out _));
            _signalInvalid |= !double.IsFinite(line);
            var signal = _signalInvalid ? double.NaN : _signal.Next(line, true);
            outputs[0] = line; outputs[1] = signal; outputs[2] = line - signal;
        }
        public void Reset() { _high.Reset(); _low.Reset(); _fast.Reset(); _slow.Reset(); _signal.Reset(); _signalInvalid = false; }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }
    }
}
#endif
