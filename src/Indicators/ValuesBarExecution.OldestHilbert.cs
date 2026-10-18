#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class OldestHilbertValueState : IIndicatorState
    {
        private readonly double[] _prices = new double[7], _means = new double[4];
        private int _priceCursor, _priceCount, _meanCursor;
        private long _count;
        private double _period, _olderSmooth, _newerSmooth;

        public void Reset()
        {
            Array.Clear(_prices); Array.Clear(_means);
            _priceCursor = _priceCount = _meanCursor = 0; _count = 0;
            _period = _olderSmooth = _newerSmooth = 0;
        }

        public double Update(in Bar bar)
        {
            _prices[_priceCursor] = bar.Close;
            if (++_priceCursor == 7) _priceCursor = 0;
            if (_priceCount < 7) _priceCount++;
            double mean = bar.Close;
            if (++_count > 5)
            {
                _period = .2 * 6 + .8 * _period;
                double smooth = .33 * _period + .67 * _olderSmooth;
                _olderSmooth = _newerSmooth; _newerSmooth = smooth;
                int cycle = (int)(smooth + .5);
                if (cycle > 0)
                {
                    var sum = new ExactMeanAccumulator();
                    int oldest = _priceCount == 7 ? _priceCursor : 0;
                    for (int i = 0; i < Math.Min(cycle, _priceCount); i++)
                        sum.Add(_prices[(oldest + i) % 7]);
                    // Preserve the full-cycle divisor even before that many
                    // prices exist; the native convention reads oldest first.
                    mean = sum.Mean(cycle);
                }
            }
            _means[_meanCursor] = mean;
            if (++_meanCursor == 4) _meanCursor = 0;
            if (_count < 11) return bar.Close;
            var weighted = new ExactMeanAccumulator();
            for (int i = 0; i < 4; i++) weighted.Add(_means[(_meanCursor + i) % 4], 4 - i);
            return weighted.Mean(10);
        }
    }
}
#endif
