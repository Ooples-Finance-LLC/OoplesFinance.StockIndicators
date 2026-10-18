#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class AlligatorState : IMultiOutputState
    {
        private sealed class Line(int period, int offset)
        {
            private readonly double[] _pending = new double[offset];
            private readonly ExactMeanAccumulator.ProductCoefficient _weight = new(period - 1);
            private ExactMeanAccumulator _seed;
            private double _previous;
            private int _observed, _filled, _head;

            internal void Reset()
            {
                _seed = default; _previous = 0; _observed = _filled = _head = 0;
            }

            internal (double Value, double Present) Update(double price)
            {
                if (_observed < period)
                {
                    _seed.Add(price);
                    if (++_observed < period) return (0, 0);
                    _previous = _seed.Mean(period); _seed = default;
                }
                else
                {
                    var numerator = new ExactMeanAccumulator();
                    numerator.AddProduct(new ExactMeanAccumulator.ProductFactor(_previous), _weight);
                    numerator.Add(price);
                    _previous = numerator.Mean(period);
                }
                if (_filled < _pending.Length)
                {
                    _pending[_filled++] = _previous;
                    return (0, 0);
                }
                double delayed = _pending[_head];
                _pending[_head] = _previous;
                if (++_head == _pending.Length) _head = 0;
                return (delayed, 1);
            }
        }

        private readonly Line?[] _lines;
        internal AlligatorState(AlligatorWithDetails owner, int count)
        {
            // No unpublished prefix can overflow: seed/updates are convex means
            // of finite midpoints. A line beyond this run needs no delay storage.
            Line? Create(int period, int offset) => (long)period - 1 + offset < count ? new(period, offset) : null;
            _lines = [Create(owner.JawPeriod, owner.JawOffset), Create(owner.TeethPeriod, owner.TeethOffset), Create(owner.LipsPeriod, owner.LipsOffset)];
        }
        public void Reset() { foreach (var line in _lines) line?.Reset(); }
        public void Update(in Bar bar, Span<double> output)
        {
            var midpoint = new ExactMeanAccumulator(); midpoint.Add(bar.High); midpoint.Add(bar.Low);
            double price = midpoint.Mean(2);
            for (int slot = 0; slot < 3; slot++)
                (output[slot], output[slot + 3]) = _lines[slot]?.Update(price) ?? (0d, 0d);
        }
    }
}
#endif
