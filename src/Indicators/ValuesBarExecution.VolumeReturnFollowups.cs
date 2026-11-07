#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedNormalizedVolumeState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double>? _volumes;
        private ExactMeanAccumulator _sum;
        internal GeneratedNormalizedVolumeState(int length, int count)
        {
            _length = Math.Max(1, length);
            if (_length <= count) _volumes = new(_length);
        }
        public double Update(in Bar bar)
        {
            if (_volumes is null) return 0;
            _sum.Add(bar.Volume);
            if (_volumes.Count == _length) _sum.Add(_volumes[0], -1);
            var numerator = new ExactMeanAccumulator(); numerator.Add(bar.Volume, _length);
            var result = _volumes.Count + 1 < _length ? 0 : numerator.Ratio(_sum);
            _volumes.TryAdd(bar.Volume, out _);
            return result;
        }
        public void Reset() { _sum = default; _volumes?.Clear(); }
        public void Dispose() => _volumes?.Dispose();
    }
    private sealed class GeneratedOcHistogramState(int length) : IIndicatorState, IDisposable
    {
        private readonly OpenCloseHistogramWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedModifiedPriceVolumeTrendState(int length) : IMultiOutputState, IDisposable
    {
        private readonly PriceVolumeTrendWindow _window = new(MovingAvgType.SimpleMovingAverage, length, true);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Reset() => _window.Reset();
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
}
#endif
