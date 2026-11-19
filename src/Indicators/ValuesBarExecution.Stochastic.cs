#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedStochasticWindow : IDisposable
    {
        private readonly RollingWindowMax _high;
        private readonly RollingWindowMin _low;
        private readonly IMovingAverageSmoother? _first, _second;
        public GeneratedStochasticWindow(int length, int firstLength, int secondLength, MovingAvgType kind, int count, bool slow)
        {
            var capacity = Math.Max(1, Math.Min(length, count));
            _high = new(capacity); _low = new(capacity);
            _first = Mean(firstLength, kind, count);
            if (slow) _second = Mean(secondLength, kind, count);
        }
        private static IMovingAverageSmoother? Mean(int length, MovingAvgType kind, int count)
        {
            length = Math.Max(1, length);
            // Match StochasticSmooth's full-period SMA warmup without allocating
            // unreachable period-sized storage. The stochastic input is finite.
            return kind == MovingAvgType.SimpleMovingAverage
                ? length > count ? null : new RoundedSimpleMovingAverageSmoother(length)
                : MovingAverageSmootherFactory.Create(kind, length);
        }
        public (double K, double D, double Slow) Next(in Bar bar)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            var k = ClampedRangePosition.Percent(bar.Close, low, high);
            var d = _first?.Next(k, true) ?? 0;
            return (k, d, _second?.Next(d, true) ?? 0);
        }
        public void Reset() { _high.Reset(); _low.Reset(); _first?.Reset(); _second?.Reset(); }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _first?.Dispose(); _second?.Dispose(); }
    }
    private sealed class GeneratedStochasticState(int length, int firstLength, int secondLength, MovingAvgType kind, int layout, int count) : IMultiOutputState, IDisposable
    {
        private readonly GeneratedStochasticWindow _window = new(length, firstLength, secondLength, kind, count, layout != 1);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(in bar);
            outputs[0] = layout == 2 ? point.D : point.K;
            outputs[1] = layout == 2 ? point.Slow : point.D;
            if (layout == 0) outputs[2] = point.Slow;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedStochasticDState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly GeneratedStochasticWindow _window = new(length, 3, 1, MovingAvgType.SimpleMovingAverage, count, false);
        public double Update(in Bar bar) => _window.Next(in bar).D;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
