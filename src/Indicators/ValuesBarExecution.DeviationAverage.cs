#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class DeviationAverageValueState : IIndicatorState
    {
        private readonly int _period;
        private readonly double _scale, _c1, _c2, _c3;
        private readonly Square[] _window;
        private JurikDyadic _average, _zero, _filter, _olderFilter;
        private BigInteger _sum;
        private int _position, _seen, _grid;
        private bool _seeded;
        private readonly record struct Square(BigInteger Integer, int Exponent);

        internal DeviationAverageValueState(int period, double scale, int count)
        {
            _period = period; _scale = scale;
            _window = new Square[Math.Min(period, Math.Max(1, count - 1))];
            double a = Math.Exp(-1.414 * Math.PI / (.5 * period));
            double b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
            double c = -a * a;
            _c1 = 1 - b - c; _c2 = b; _c3 = c;
        }

        public void Reset()
        {
            _average = _zero = _filter = _olderFilter = default;
            _sum = default; _position = _seen = _grid = 0; _seeded = false;
        }

        public double Update(in Bar bar)
        {
            var price = JurikDyadic.FromDouble(bar.Close);
            if (!_seeded)
            {
                _seeded = true; _average = price;
                return bar.Close; // Preserve the original seed's signed zero.
            }
            var zero = price; zero.Subtract(_average); zero = zero.Rounded();
            var filter = zero; filter.AddExact(_zero); filter.Multiply(_c1);
            filter.ScaleByPowerOfTwo(-1);
            var term = _filter; term.Multiply(_c2); filter.AddExact(term);
            term = _olderFilter; term.Multiply(_c3); filter.AddExact(term);
            filter = filter.Rounded(); // One rounding after all three terms.

            Square square = FilterSquare(in filter);
            if (_seen == _window.Length)
            {
                var expired = _window[_position];
                if (!expired.Integer.IsZero) _sum -= expired.Integer << (expired.Exponent - _grid);
            }
            else _seen++;
            _window[_position] = square;
            if (++_position == _window.Length) _position = 0;
            if (!square.Integer.IsZero)
            {
                if (_sum.IsZero) { _sum = square.Integer; _grid = square.Exponent; }
                else if (square.Exponent < _grid)
                { _sum = (_sum << (_grid - square.Exponent)) + square.Integer; _grid = square.Exponent; }
                else _sum += square.Integer << (square.Exponent - _grid);
            }
            double normalized = _sum.IsZero || square.Integer.IsZero ? 0
                : ExactPopulationDeviation.ScaledRootRatio(square.Integer * _period, _sum, square.Exponent - _grid);
            double gain = Math.Max(.1, Math.Min(1, _scale * normalized * 5 / _period));
            if (JurikDyadic.TryBlend(_average, price, gain, out var blended)) _average = blended;
            else
            {
                price.Subtract(_average); price.Multiply(gain); price.AddExact(_average);
                _average = price.Rounded();
            }
            _zero = zero; _olderFilter = _filter; _filter = filter;
            return _average.Mean(1);
        }

        private static Square FilterSquare(in JurikDyadic filter)
        {
            long integer;
            int exponent;
            if (filter.TryDouble(out double value))
            {
                (integer, exponent) = ExactMeanAccumulator.DecomposeFinite(value);
                if (integer == 0) return default;
            }
            else
            {
                // Rounded unpublished filters may exceed binary64 range. Keep
                // their 53-bit significand and extended exponent before squaring
                // and normalizing; never turn an intermediate overflow into Inf.
                BigInteger units = filter.RoundedUnits();
                if (units.IsZero) return default;
                int zeros = ExactMeanAccumulator.TrailingBinaryZeros(units);
                integer = (long)(units >> zeros); exponent = zeros - 1074;
            }
            return new((BigInteger)integer * integer, 2 * exponent);
        }
    }
}
#endif
