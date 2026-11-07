#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedPivotAverageState : IMultiOutputState, IDisposable
    {
        private readonly StrengthAverage _first, _second, _third;
        private readonly InputLength _inputLength;
        private readonly double[] _current = new double[6];
        private DateTime? _period;
        private double _high, _low, _close;
        public GeneratedPivotAverageState(int length, InputLength inputLength, int count)
        {
            _inputLength = inputLength;
            _first = new(MovingAvgType.SimpleMovingAverage, length, count);
            _second = new(MovingAvgType.SimpleMovingAverage, length, count);
            _third = new(MovingAvgType.SimpleMovingAverage, length, count);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var period = CalculationsHelper.InputPeriodStart(bar.Time, _inputLength);
            var changed = _period != period;
            if (changed)
            {
                var (first, second, third) = PivotAverageMath.Values(_high, _low, _close, bar.Open);
                _current[0] = first; _current[1] = _first.Next(new(first), true).Mantissa;
                _current[2] = second; _current[3] = _second.Next(new(second), true).Mantissa;
                _current[4] = third; _current[5] = _third.Next(new(third), true).Mantissa;
            }
            _high = changed ? bar.High : Math.Max(_high, bar.High);
            _low = changed ? bar.Low : Math.Min(_low, bar.Low);
            _close = bar.Close; _period = period; _current.AsSpan().CopyTo(outputs);
        }
        public void Reset()
        {
            _first.Reset(); _second.Reset(); _third.Reset(); _period = null;
            _high = _low = _close = 0; Array.Clear(_current, 0, _current.Length);
        }
        public void Dispose() { _first.Dispose(); _second.Dispose(); _third.Dispose(); }
    }
}
#endif
