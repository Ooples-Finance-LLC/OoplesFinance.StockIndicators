#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines.Simd;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // ALMA uses the original normalized Gaussian coefficients, including its
    // asymmetric oldest-first convention. Only complete batch windows use SIMD.
    private sealed class AlmaGridBatchState
    {
        private const int Block = 128;
        private const int PriceLimit = 2047;
        private readonly ArnaudLegouxWindow.State _original;
        private readonly int _period, _count;
        private readonly BoundedOutput _target;
        private readonly double[] _closes, _prices, _low, _high, _lowWeights, _highWeights;
        private readonly double[] _scratch = new double[2];
        private int _fill, _carry, _received, _origin, _weightGrid;
        private UInt128 _mass;
        private bool _weightsReady, _originalOnly;

        internal AlmaGridBatchState(ArnaudLegouxWindow.State original, int period,
            int count, BoundedOutput target)
        {
            _original = original; _period = period; _count = count; _target = target;
            _origin = target.Origin;
            int capacity = Block + period - 1;
            _closes = new double[capacity]; _prices = new double[capacity];
            _low = new double[capacity]; _high = new double[capacity];
            _lowWeights = new double[period]; _highWeights = new double[period];
        }

        internal double Update(in Bar bar)
        {
            if (_originalOnly) return Original(in bar, _target.Origin + _received++);
            _closes[_fill++] = bar.Close;
            _received++;
            if (_received < _period)
            {
                // Expanding ALMA recomputes its weights at every startup size;
                // prefixing the final weights would change the public result.
                _carry = _fill;
                return Original(in bar, _origin + _received - 1);
            }
            if (!_weightsReady)
            {
                // Preserve the first full row's original state/exception order
                // before requesting factors or doing any buffered publication.
                double value = Original(in bar, _target.Origin + _received - 1);
                if (!CertifyWeights())
                {
                    _originalOnly = true;
                    return value;
                }
                _weightsReady = true;
                Array.Copy(_closes, 1, _closes, 0, _period - 1);
                _origin++;
                _fill = _carry = _period - 1;
                return value;
            }
            if (_fill < _closes.Length && _received < _count) return 0;
            double last = Flush();
            int retain = Math.Min(_period - 1, _fill);
            Array.Copy(_closes, _fill - retain, _closes, 0, retain);
            _origin += _fill - retain;
            _fill = _carry = retain;
            return last;
        }

        private double Original(in Bar bar, int row)
        {
            _original.Update(in bar, _scratch);
            if (row >= _target.WriteStart) _target.Values[1][row] = _scratch[1];
            return _scratch[0];
        }

        private bool CertifyWeights()
        {
            var factors = _original.PrepareBatchWeights();
            int grid = int.MaxValue;
            foreach (var factor in factors)
            {
                if (factor.Mantissa < 0) return false;
                if (factor.Mantissa != 0) grid = Math.Min(grid, factor.Scale - 1074);
            }
            if (grid == int.MaxValue) return false;
            UInt128 mass = 0, limit = (UInt128)1 << 72;
            for (int lag = 0; lag < _period; lag++)
            {
                // State factors are oldest-first; SIMD lag zero is newest.
                var factor = factors[_period - lag - 1];
                int shift = factor.Scale - 1074 - grid;
                if (factor.Mantissa != 0 && (shift < 0 || shift >= 72)) return false;
                UInt128 value = factor.Mantissa == 0 ? 0 : (UInt128)factor.Mantissa << shift;
                if (value >= limit) return false;
                mass += value;
                _lowWeights[lag] = (double)(ulong)(value & (((UInt128)1 << 36) - 1));
                _highWeights[lag] = (double)(ulong)(value >> 36);
            }
            if (mass == 0 || mass >= (UInt128)1 << 78) return false;
            _mass = mass; _weightGrid = grid;
            return true;
        }

        private double Flush()
        {
            int grid = int.MaxValue;
            double maximum = 0;
            for (int i = 0; i < _fill; i++) IncludeIntegerGrid(_closes[i], ref grid, ref maximum);
            if (grid == int.MaxValue) grid = 0;
            if (Math.ScaleB(maximum, -grid) > PriceLimit) return Replay();
            for (int i = 0; i < _fill; i++) _prices[i] = Math.ScaleB(_closes[i], -grid);
            int first = Math.Max(_carry, Math.Max(0, _target.WriteStart - _origin));
            Array.Clear(_low, first, _fill - first); Array.Clear(_high, first, _fill - first);
            for (int lag = 0; lag < _period; lag++)
            {
                int length = _fill - first;
                var low = _low.AsSpan(first, length); var high = _high.AsSpan(first, length);
                SimdKernels.ScalarMultiplyAdd(low, _prices.AsSpan(first - lag, length), _lowWeights[lag], low);
                SimdKernels.ScalarMultiplyAdd(high, _prices.AsSpan(first - lag, length), _highWeights[lag], high);
            }
            var denominator = JurikDyadic.FromBinaryProduct((Int128)_mass, _weightGrid);
            double last = 0;
            for (int i = first; i < _fill; i++)
            {
                // P<=64, |priceInt|<=2047, coefficientInt<2^72:
                // each signed36-bit limb partial is strictly below2^53.
                // Integer recombination is below2^89 and fits Int128.
                Int128 integer = ((Int128)(long)_high[i] << 36) + (long)_low[i];
                var numerator = JurikDyadic.FromBinaryProduct(integer, grid + _weightGrid);
                Publish(i, numerator.Ratio(denominator), 1, ref last);
            }
            return last;
        }

        private double Replay()
        {
            _original.Reset();
            double last = 0;
            for (int i = 0; i < _fill; i++)
            {
                var bar = new Bar(default, 0, 0, 0, _closes[i], 0);
                _original.Update(in bar, _scratch);
                if (i >= _carry) Publish(i, _scratch[0], _scratch[1], ref last);
            }
            return last;
        }

        private void Publish(int index, double value, double present, ref double last)
        {
            int row = _origin + index;
            if (row < _target.WriteStart) return;
            _target.Values[1][row] = present;
            if (index == _fill - 1) last = value;
            else _target.Values[0][row] = value;
        }
    }
}
#endif
