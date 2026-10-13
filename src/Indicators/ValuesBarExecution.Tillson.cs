#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class TillsonValueState : IMultiOutputState
    {
        private readonly int _period;
        private readonly TillsonSeed _seed;
        private readonly long _first;
        private readonly BigInteger[] _coefficients;
        private readonly ExactMeanAccumulator _denominator;
        private readonly JurikDyadic _smoothingDivisor;
        private readonly double[] _stages = new double[6];
        private readonly JurikDyadic[] _sums = new JurikDyadic[6];
        private long _index = -1;

        internal TillsonValueState(int period, double factor, TillsonSeed seed, int suppression)
        {
            _period = period;
            _seed = seed;
            _first = (seed == TillsonSeed.CascadedMeans ? 6L * (period - 1) : 0) + suppression;
            _smoothingDivisor = JurikDyadic.FromDouble((long)period + 1);
            var parts = ExactMeanAccumulator.DecomposeFinite(factor);
            BigInteger n = parts.Integer, q = BigInteger.One;
            if (parts.Exponent >= 0) n <<= parts.Exponent;
            else q <<= -parts.Exponent;
            var combined = q + n;
            _coefficients = [combined * combined * combined, -3 * n * combined * combined,
                3 * n * n * combined, -n * n * n];
            var denominator = new ExactMeanAccumulator();
            denominator.Add(1, q * q * q);
            _denominator = denominator;
        }

        public void Reset()
        {
            Array.Clear(_stages);
            Array.Clear(_sums);
            _index = -1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            double value = bar.Close;
            for (int j = 0; j < 6; j++)
            {
                if (_seed == TillsonSeed.CascadedMeans)
                {
                    long start = (long)j * (_period - 1);
                    if (_index < start) return;
                    if (_index < start + _period)
                    {
                        _sums[j].Add(value);
                        if (_index != start + _period - 1) return;
                        _stages[j] = _sums[j].Mean(_period);
                        _sums[j] = default;
                    }
                    else _stages[j] = Smooth(_stages[j], value);
                }
                else if (_index == 0) _stages[j] = value;
                else if (_seed == TillsonSeed.FollowingPrefix && _index < _period)
                {
                    // The first price is deliberately excluded from this seed.
                    _sums[j].Add(value);
                    _stages[j] = _sums[j].Mean((int)_index);
                }
                else _stages[j] = Smooth(_stages[j], value);
                value = _stages[j];
            }
            if (_index < _first) return;
            // Finite raw closes and convex stage means keep every stage finite.
            // Coefficient terms may overflow separately: accumulate all four
            // exactly, then round the complete polynomial once.
            var polynomial = new ExactMeanAccumulator();
            for (int j = 0; j < 4; j++) polynomial.Add(_stages[j + 2], _coefficients[j]);
            output[0] = polynomial.Ratio(_denominator);
            output[1] = 1;
        }

        private double Smooth(double prior, double input)
        {
            var numerator = JurikDyadic.FromDouble(prior);
            numerator.Multiply(_period - 1d);
            var incoming = JurikDyadic.FromDouble(input);
            incoming.Multiply(2);
            numerator.AddExact(incoming);
            return numerator.Ratio(_smoothingDivisor);
        }
    }
}
#endif
