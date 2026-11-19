#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines.Simd;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Batch-only closed analytic indicators consume Close, just like their
    // unchanged streaming state. The generic caller still validates every Bar
    // and owns history. No caller source is read again during a block flush.
    private sealed class AnalyticGridBatchState : IIndicatorState
    {
        private const int Block = 128;
        private const int PriceLimit = 16383;
        private readonly IIndicatorState _original;
        private readonly int _period, _count;
        private readonly BoundedOutput _target;
        private readonly double[] _closes, _prices, _low, _high, _lowWeights, _highWeights;
        private readonly ulong[] _masses;
        private readonly bool _weightsCertified;
        private readonly int _weightGrid;
        private int _fill, _carry, _received, _origin;

        internal AnalyticGridBatchState(IIndicatorState original, Func<int, double> coefficient,
            int period, int count, BoundedOutput target)
        {
            _original = original; _period = period; _count = count; _target = target;
            _origin = target.Origin;
            int capacity = Block + period - 1;
            _closes = new double[capacity]; _prices = new double[capacity];
            _low = new double[capacity]; _high = new double[capacity];
            _lowWeights = new double[period]; _highWeights = new double[period];
            _masses = new ulong[period];
            _weightsCertified = CertifyWeights(coefficient, out _weightGrid);
        }

        internal bool HasCertifiedPositiveMass => _weightsCertified && _masses[_period - 1] != 0;

        public void Reset()
        {
            _fill = _carry = _received = 0; _origin = _target.Origin;
            _original.Reset();
        }

        public double Update(in Bar bar)
        {
            if (!_weightsCertified) return _original.Update(in bar);
            _closes[_fill++] = bar.Close;
            _received++;
            if (_fill < _closes.Length && _received < _count) return 0;
            double last = Flush();
            int retain = Math.Min(_period - 1, _fill);
            Array.Copy(_closes, _fill - retain, _closes, 0, retain);
            _origin += _fill - retain;
            _fill = _carry = retain;
            return last;
        }

        private bool CertifyWeights(Func<int, double> coefficient, out int grid)
        {
            grid = int.MaxValue;
            double maximum = 0;
            var weights = new double[_period];
            for (int lag = 0; lag < weights.Length; lag++)
            {
                double value = coefficient(lag);
                // Reject before decomposition; the original state retains all
                // extended/invalid-coefficient behavior on that route.
                if (!double.IsFinite(value) || value < 0) return false;
                weights[lag] = value;
                IncludeIntegerGrid(value, ref grid, ref maximum);
            }
            if (grid == int.MaxValue) grid = 0;
            if (Math.ScaleB(maximum, -grid) > (1UL << 58)) return false;
            ulong mass = 0;
            for (int lag = 0; lag < weights.Length; lag++)
            {
                // The common dyadic grid makes this conversion integral.
                ulong value = (ulong)Math.ScaleB(weights[lag], -grid);
                UInt128 sum = (UInt128)mass + value;
                if (sum > ulong.MaxValue) return false;
                mass = (ulong)sum; _masses[lag] = mass;
                _lowWeights[lag] = value & uint.MaxValue;
                _highWeights[lag] = value >> 32;
            }
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
            for (int lag = 0; lag < Math.Min(_period, _fill); lag++)
            {
                int start = Math.Max(first, lag), length = _fill - start;
                var low = _low.AsSpan(start, length); var high = _high.AsSpan(start, length);
                // ScalarMultiplyAdd is a + scalar*b. Its actual SIMD/scalar
                // loops load each a element before writing the same result
                // element, so exact a/result aliasing is safe.
                SimdKernels.ScalarMultiplyAdd(low, _prices.AsSpan(start - lag, length), _lowWeights[lag], low);
                SimdKernels.ScalarMultiplyAdd(high, _prices.AsSpan(start - lag, length), _highWeights[lag], high);
            }
            double last = 0;
            for (int i = first; i < _fill; i++)
            {
                // P<=64, |priceInt|<=16383, coeffInt<=2^58:
                // every low32/high26 product and signed partial sum is an
                // exact integer below 2^52/2^46, with or without FMA.
                // Recombination is below 2^79 and fits Int128.
                Int128 integer = ((Int128)(long)_high[i] << 32) + (long)_low[i];
                ulong mass = _masses[Math.Min(i + 1, _period) - 1];
                var numerator = JurikDyadic.FromBinaryProduct(integer, grid + _weightGrid);
                var denominator = JurikDyadic.FromBinaryProduct((Int128)mass, _weightGrid);
                // A zero available mass has zero weighted numerator, not an
                // unweighted price mean. Keep scale-aware exact rounding for
                // subnormals; ratio-then-ScaleB would round twice.
                double value = mass == 0 ? numerator.Mean(Math.Min(i + 1, _period)) : numerator.Ratio(denominator);
                Publish(i, value, ref last);
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
                double value = _original.Update(in bar);
                if (i >= _carry) Publish(i, value, ref last);
            }
            return last;
        }

        private void Publish(int index, double value, ref double last)
        {
            int row = _origin + index;
            if (row < _target.WriteStart) return;
            // Current-row publication belongs to the generic caller's return
            // assignment; only earlier validated rows are backfilled here.
            if (index == _fill - 1) last = value;
            else _target.Values[0][row] = value;
        }
    }
}
#endif
