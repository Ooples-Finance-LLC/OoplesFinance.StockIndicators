#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedObvDisparityState(int length, int signalLength, double top, double bottom) : IMultiOutputState, IDisposable
    {
        private readonly OnBalanceVolumeDisparityWindow _window = new(MovingAvgType.SimpleMovingAverage, length, signalLength, top, bottom);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line.Publish(); outputs[1] = point.SignalLine.Publish();
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedVixState : IIndicatorState, IDisposable
    {
        private readonly SimpleMovingAverageSmoother? _average;
        private double _count;
        public GeneratedVixState(int length, int count)
        {
            // A window beyond the entire batch only publishes zero warmup values.
            if (Math.Max(1, length) <= count) _average = new(length);
        }
        public double Update(in Bar bar)
        {
            var average = _average?.Next(bar.Close, true) ?? 0;
            // Preserve the original sticky sign when price crosses the average.
            _count = bar.Close > average && _count >= 0 ? _count + 1
                : bar.Close <= average && _count <= 0 ? _count - 1 : _count;
            return _count;
        }
        public void Reset() { _count = 0; _average?.Reset(); }
        public void Dispose() => _average?.Dispose();
    }
}
#endif
