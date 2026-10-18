#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Retain the original sequential Wilder ATR and exact offset expression;
    // only rolling extrema storage changes from repeated scans to bounded deques.
    internal sealed class ChandelierState : IMultiOutputState
    {
        private readonly WindowChandelierExit _owner;
        private readonly SeededAtrWindow _atr;
        private readonly ExtremeDeque? _high, _low;
        private long _index = -1;

        internal ChandelierState(WindowChandelierExit owner, int capacity)
        {
            _owner = owner;
            _atr = new(owner.Period);
            int window = Math.Min(owner.Period, capacity);
            if (owner.Selection != ChandelierExitSelection.Short) _high = new(window, true);
            if (owner.Selection != ChandelierExitSelection.Long) _low = new(window, false);
        }

        public void Reset()
        {
            _atr.Reset(); _high?.Reset(); _low?.Reset(); _index = -1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear(); _atr.Add(bar); _index++;
            long first = _index - _owner.Period + 1;
            _high?.Add(_index, bar.High, first);
            _low?.Add(_index, bar.Low, first);
            if (!_atr.HasAverage) return;
            var product = new ExactMeanAccumulator();
            product.AddProduct(_atr.Average.Mantissa, _owner.Multiplier);
            product.ScaleByPowerOfTwo(_atr.Average.UpperShift);
            if (_high is not null)
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(_owner.ZeroFloorHigh ? Math.Max(0, _high.Value) : _high.Value);
                sum.Subtract(product);
                output[0] = sum.Mean(1); output[2] = 1;
            }
            if (_low is not null)
            {
                var sum = product; sum.Add(_low.Value);
                output[1] = sum.Mean(1); output[3] = 1;
            }
        }
    }
}
#endif
