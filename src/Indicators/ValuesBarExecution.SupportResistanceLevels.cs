#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSupportResistanceLevelsState : IMultiOutputState, IDisposable
    {
        private readonly SimpleMovingAverageSmoother? _average;
        private readonly RollingWindowMax _high;
        private readonly RollingWindowMin _low;
        private double _previous, _previousAverage, _support, _resistance;
        private bool _started;
        public GeneratedSupportResistanceLevelsState(int length, int count)
        {
            // A full-window mean is zero throughout a batch shorter than its period.
            if (length <= count) _average = new(length);
            var capacity = Math.Max(1, Math.Min(length, count));
            _high = new(capacity); _low = new(capacity);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            if (!_started || _previous < _previousAverage && bar.Close >= _previousAverage) _support = low;
            if (!_started || _previous > _previousAverage && bar.Close <= _previousAverage) _resistance = high;
            outputs[0] = _support; outputs[1] = _resistance;
            _previousAverage = _average?.Next(bar.Close, true) ?? 0;
            _previous = bar.Close; _started = true;
        }
        public void Reset()
        {
            _average?.Reset(); _high.Reset(); _low.Reset();
            _previous = _previousAverage = _support = _resistance = 0; _started = false;
        }
        public void Dispose() { _average?.Dispose(); _high.Dispose(); _low.Dispose(); }
    }
}
#endif
