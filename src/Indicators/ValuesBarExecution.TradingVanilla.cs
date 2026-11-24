#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedTradingStochastic(int length, int smooth, int count) : IDisposable
    {
        private readonly RollingWindowMax _high = new(Math.Max(1, Math.Min(Math.Max(1, length), count)));
        private readonly RollingWindowMin _low = new(Math.Max(1, Math.Min(Math.Max(1, length), count)));
        private readonly WmaValueState _mean = new(Math.Max(1, smooth), count);
        public double Next(in Bar bar)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            var k = ClampedRangePosition.Percent(bar.Close, low, high);
            var sample = new Bar(default, k, k, k, k, 0);
            return _mean.Update(in sample);
        }
        public void Reset() { _high.Reset(); _low.Reset(); _mean.Reset(); }
        public void Dispose() { _high.Dispose(); _low.Dispose(); }
    }
    private sealed class GeneratedTradingSimplerState(int first, int second, int smooth, double threshold, double limit, int count) : IIndicatorState, IDisposable
    {
        private readonly PriceRsiWindow _rsi = new(MovingAvgType.WeightedMovingAverage, first, count);
        private readonly GeneratedTradingStochastic _first = new(second, smooth, count), _second = new(first, smooth, count);
        public double Update(in Bar bar)
        {
            // Each component consumes the same original candle; length3 is unused
            // by the public formula and must not alter its output here.
            var rsi = _rsi.Next(bar.Close, true) - threshold;
            var firstStochastic = _first.Next(in bar) - threshold;
            var secondStochastic = _second.Next(in bar) - threshold;
            var up = rsi > limit && firstStochastic > limit && secondStochastic > limit ? secondStochastic : 0;
            var down = rsi < limit && firstStochastic < limit && secondStochastic < limit ? secondStochastic : 0;
            return up - down;
        }
        public void Reset() { _rsi.Reset(); _first.Reset(); _second.Reset(); }
        public void Dispose() { _rsi.Dispose(); _first.Dispose(); _second.Dispose(); }
    }
    private sealed class GeneratedVanillaAbcdState : IIndicatorState
    {
        private double _previous, _second, _third, _state;
        public double Update(in Bar bar)
        {
            var up = _third > _second && _previous > _second && bar.Close < _second;
            var down = _third < _second && _previous < _second && bar.Close > _second;
            var next = up ? 1 : down ? 0 : _state;
            var result = next - _state;
            _state = next; _third = _second; _second = _previous; _previous = bar.Close;
            return result;
        }
        public void Reset() => _previous = _second = _third = _state = 0;
    }
}
#endif
