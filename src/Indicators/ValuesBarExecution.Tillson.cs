#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class TillsonValueState : IMultiOutputState
    {
        private readonly int _period;
        private readonly CertifiedTillson.WordSmoother _wordSmoother;
        private readonly TillsonSeed _seed;
        private readonly long _first;
        private readonly double _factor;
        private readonly JurikDyadic _smoothingDivisor;
        private readonly double[] _stages = new double[6];
        private readonly JurikDyadic[] _sums = new JurikDyadic[6];
        private long _index = -1;
        private ulong _wordExponent;

        internal TillsonValueState(int period, double factor, TillsonSeed seed, int suppression)
        {
            _period = period;
            _wordSmoother = new CertifiedTillson.WordSmoother(period);
            _seed = seed;
            _first = (seed == TillsonSeed.CascadedMeans ? 6L * (period - 1) : 0) + suppression;
            _smoothingDivisor = JurikDyadic.FromDouble((long)period + 1);
            _factor = factor;
        }

        public void Reset()
        {
            Array.Clear(_stages);
            Array.Clear(_sums);
            _index = -1;
            _wordExponent = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            double value = bar.Close;
            bool cached = _wordExponent != 0
                && unchecked((ulong)BitConverter.DoubleToInt64Bits(value)) >> 52 == _wordExponent;
            if (!cached) _wordExponent = 0;
            for (int j = 0; j < _stages.Length; j++)
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
                    else _stages[j] = Smooth(_stages[j], value, cached);
                }
                else if (_index == 0) _stages[j] = value;
                else if (_seed == TillsonSeed.FollowingPrefix && _index < _period)
                {
                    // The first price is deliberately excluded from this seed.
                    _sums[j].Add(value);
                    _stages[j] = _sums[j].Mean((int)_index);
                }
                else _stages[j] = Smooth(_stages[j], value, cached);
                value = _stages[j];
            }
            // Prefix seeds are not convex updates of the prior published
            // stage. Establish the invariant only after all seed windows end.
            // CascadedMeans returns above until every stage has been seeded.
            if (_wordExponent == 0 && _wordSmoother.IsSupported
                && (_seed == TillsonSeed.FirstPrice || _index >= _period - 1L))
                _wordExponent = CommonWordExponent();
            if (_index < _first) return;
            // Finite raw closes and convex stage means keep every stage finite.
            output[0] = Combine(_stages[2], _stages[3], _stages[4], _stages[5], _factor);
            output[1] = 1;
        }

        internal static double Combine(double third, double fourth, double fifth, double sixth, double factor)
        {
            if (CertifiedTillson.TryCombine(third, fourth, fifth, sixth, factor, out double certified))
                return certified;
            // Exact finite differences give E3 + a*(3*d1 + a*(3*d2 + a*d3)).
            // Form the differences with small integer weights before the three
            // factor products. Wide factors/cancellation retain arbitrary precision.
            var first = new ExactMeanAccumulator();
            first.Add(third, 3); first.Add(fourth, -3);
            var second = new ExactMeanAccumulator();
            second.Add(third, 3); second.Add(fourth, -6); second.Add(fifth, 3);
            var polynomial = new ExactMeanAccumulator();
            polynomial.Add(third); polynomial.Add(fourth, -3);
            polynomial.Add(fifth, 3); polynomial.Add(sixth, -1);
            polynomial.Multiply(factor);
            polynomial.AddExact(second);
            polynomial.Multiply(factor);
            polynomial.AddExact(first);
            polynomial.Multiply(factor);
            polynomial.Add(third);
            return polynomial.Mean(1);
        }

        private ulong CommonWordExponent()
        {
            ulong exponent = unchecked((ulong)BitConverter.DoubleToInt64Bits(_stages[0])) >> 52;
            if (exponent is 0 or > 2046) return 0;
            for (int j = 1; j < 6; j++)
                if (unchecked((ulong)BitConverter.DoubleToInt64Bits(_stages[j])) >> 52 != exponent) return 0;
            return exponent;
        }

        private double Smooth(double prior, double input, bool cached)
        {
            if (cached) return _wordSmoother.SmoothSameBinade(
                unchecked((ulong)BitConverter.DoubleToInt64Bits(prior)),
                unchecked((ulong)BitConverter.DoubleToInt64Bits(input)), _wordExponent);
            if (_wordSmoother.TrySmooth(prior, input, out double wordResult)) return wordResult;
            if (CertifiedTillson.TrySmooth(prior, input, _period, out double certified))
                return certified;
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
