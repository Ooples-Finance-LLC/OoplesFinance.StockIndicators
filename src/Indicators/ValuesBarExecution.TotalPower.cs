#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedTotalPowerState : IMultiOutputState, IDisposable
    {
        private readonly ExponentialMovingAverageSmoother _average;
        private readonly RollingWindowSum _bull, _bear;
        private readonly int _length;
        public GeneratedTotalPowerState(int length, int average, int count)
        {
            _length = length; _average = new(Math.Max(1, average));
            var capacity = Math.Max(1, Math.Min(length, count));
            _bull = new(capacity); _bear = new(capacity);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var average = _average.Next(bar.Close, true);
            var bull = _bull.Add(bar.High - average > 0 ? 1 : 0, out _);
            var bear = _bear.Add(bar.Low - average < 0 ? 1 : 0, out _);
            outputs[0] = 100 * Math.Abs(bull - bear) / _length;
            outputs[1] = 100 * bull / _length; outputs[2] = 100 * bear / _length;
        }
        public void Reset() { _average.Reset(); _bull.Reset(); _bear.Reset(); }
        public void Dispose() { _average.Dispose(); _bull.Dispose(); _bear.Dispose(); }
    }
}
#endif
