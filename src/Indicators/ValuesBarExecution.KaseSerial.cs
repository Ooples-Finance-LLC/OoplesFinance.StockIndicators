#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedKaseSerialState : IMultiOutputState, IDisposable
    {
        private readonly int _period;
        private readonly Streaming.RollingStandardDeviation? _deviation;
        private readonly PooledRingBuffer<double>? _high, _low;
        private double _previous;
        public GeneratedKaseSerialState(int length, int count)
        {
            _period = Math.Max(1, length);
            // The existing deviation returns zero until its entire period is
            // observed. An unreachable warmup needs no period-sized storage.
            if (_period > count) return;
            _deviation = new(_period); _high = new(_period); _low = new(_period);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_deviation is null) { outputs[0] = outputs[1] = 0; return; }
            var volatility = _deviation.Next(StableLogRatio.OfSameSign(bar.Close, _previous), true);
            var previousHigh = _high!.Count >= _period ? _high[0] : 0;
            var previousLow = _low!.Count >= _period ? _low[0] : 0;
            var up = StableLogRatio.OfSameSign(bar.High, previousLow);
            var down = StableLogRatio.OfSameSign(bar.Low, previousHigh);
            outputs[0] = volatility != 0 ? up / volatility : 0;
            outputs[1] = volatility != 0 ? down / volatility : 0;
            _high.TryAdd(bar.High, out _); _low.TryAdd(bar.Low, out _); _previous = bar.Close;
        }
        public void Reset() { _deviation?.Reset(); _high?.Clear(); _low?.Clear(); _previous = 0; }
        public void Dispose() { _deviation?.Dispose(); _high?.Dispose(); _low?.Dispose(); }
    }
}
#endif
