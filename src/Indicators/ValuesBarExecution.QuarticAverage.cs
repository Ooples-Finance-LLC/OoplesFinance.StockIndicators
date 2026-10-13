#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class QuarticValueState : IMultiOutputState
    {
        private readonly int _period, _scaleExponent;
        private readonly McGinleyStartup _startup;
        private readonly BigInteger _scale;
        private long _index = -1, _start = 1;
        private double _previous;

        internal QuarticValueState(int period, double factor, McGinleyStartup startup)
        {
            _period = period;
            _startup = startup;
            var scale = ExactMeanAccumulator.DecomposeFinite(factor);
            _scale = (BigInteger)scale.Integer * period;
            _scaleExponent = scale.Exponent;
        }

        public void Reset()
        {
            _index = -1;
            _start = 1;
            _previous = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            double value = bar.Close;
            if (++_index == 0)
            {
                _previous = value;
                if (_startup == McGinleyStartup.Immediate)
                {
                    output[0] = value;
                    output[1] = 1;
                    return;
                }
            }
            if (_startup == McGinleyStartup.ResetDelay && _previous == 0)
            {
                _previous = value;
                _start = _index + _period;
                return;
            }

            double next;
            if (_previous == 0)
            {
                var price = ExactMeanAccumulator.DecomposeFinite(value);
                next = ExactMeanAccumulator.ScaledRatio(price.Integer, _scale, price.Exponent - _scaleExponent);
            }
            else if (value == 0)
                next = _previous > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            else if (value == _previous)
                next = value;
            else
            {
                var price = ExactMeanAccumulator.DecomposeFinite(value);
                var old = ExactMeanAccumulator.DecomposeFinite(_previous);
                int differenceExponent = Math.Min(price.Exponent, old.Exponent);
                var difference = ((BigInteger)price.Integer << (price.Exponent - differenceExponent))
                    - ((BigInteger)old.Integer << (old.Exponent - differenceExponent));
                var priceSquared = (BigInteger)price.Integer * price.Integer;
                var oldSquared = (BigInteger)old.Integer * old.Integer;
                var divisor = _scale * priceSquared * priceSquared;
                var correction = difference * oldSquared * oldSquared;
                int divisorExponent = _scaleExponent + 4 * price.Exponent;
                int previousExponent = old.Exponent + divisorExponent;
                int correctionExponent = differenceExponent + 4 * old.Exponent;
                int numeratorExponent = Math.Min(previousExponent, correctionExponent);
                // m + (p-m)*m^4/(factor*period*p^4), evaluated as one
                // exact rational. Keep binary powers out of the significand
                // products; align only the two numerator terms. This also
                // preserves cancellation, subnormal ties and overflow.
                var numerator = ((old.Integer * divisor) << (previousExponent - numeratorExponent))
                    + (correction << (correctionExponent - numeratorExponent));
                next = ExactMeanAccumulator.ScaledRatio(numerator, divisor, numeratorExponent - divisorExponent);
            }
            if (!double.IsFinite(next))
            {
                output[0] = next;
                return;
            }
            _previous = next;
            if (_startup == McGinleyStartup.Immediate || _index >= _start)
            {
                output[0] = next;
                output[1] = 1;
            }
        }
    }
}
#endif
