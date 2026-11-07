#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedGOscillatorState : IIndicatorState, IDisposable
    {
        private readonly double[] _high;
        private readonly double[] _low;
        private readonly int _period;
        private readonly double _weight;
        private double _previous;
        private double _prefixHigh, _prefixLow;
        private int _seen, _index;
        public GeneratedGOscillatorState(int length, int count)
        {
            _period = Math.Max(1, length);
            _weight = 100.0 / _period;
            var capacity = Math.Max(1, Math.Min(_period, count));
            _high = new double[capacity];
            _low = new double[capacity];
        }
        public double Update(in Bar bar)
        {
            var value = bar.Close > _previous ? _weight : 0;
            _previous = bar.Close;
            // Match ComputeGOscillatorFast's RollingSum prefix arithmetic,
            // retaining only the prefix pairs that this batch can subtract.
            (_prefixHigh, _prefixLow) = CompensatedSum.Add(_prefixHigh, _prefixLow, value);
            var result = _seen >= _period
                ? CompensatedSum.Difference(_prefixHigh, _prefixLow, _high[_index], _low[_index])
                : _prefixHigh + _prefixLow;
            _high[_index] = _prefixHigh;
            _low[_index] = _prefixLow;
            if (++_index == _high.Length) _index = 0;
            _seen++;
            return result;
        }
        public void Reset()
        {
            _previous = _prefixHigh = _prefixLow = 0;
            _seen = _index = 0;
        }
        public void Dispose() { }
    }
}
#endif
