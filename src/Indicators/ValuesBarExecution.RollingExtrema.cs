#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRollingCloseExtremeState(int period, int count, bool maximum) : IIndicatorState
    {
        // A window covering the complete batch only needs its running extreme.
        // Otherwise the deque retains the oldest equal value, including signed zero.
        private readonly ExtremeDeque? _window = period < count ? new(period, maximum) : null;
        private long _index;
        private double _best;

        public double Update(in Bar bar)
        {
            if (_window is not null)
            {
                _window.Add(_index, bar.Close, _index - period + 1);
                _best = _window.Value;
            }
            else if (_index == 0 || (maximum ? bar.Close > _best : bar.Close < _best))
            {
                _best = bar.Close;
            }
            _index++;
            return _best;
        }

        public void Reset()
        {
            _window?.Reset();
            _index = 0;
            _best = 0;
        }
    }
}
#endif
