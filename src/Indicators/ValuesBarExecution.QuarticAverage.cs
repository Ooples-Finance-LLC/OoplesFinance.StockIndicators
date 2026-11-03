#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class QuarticValueState : IMultiOutputState
    {
        private static readonly double MinimumCertifiedMagnitude = Math.ScaleB(1d, -200),
            MaximumCertifiedMagnitude = Math.ScaleB(1d, 200);
        private readonly int _period, _scaleExponent;
        private readonly McGinleyStartup _startup;
        private readonly BigInteger _scale;
        private readonly double _inverseScaleLow, _inverseScaleHigh;
        private long _index = -1, _start = 1;
        private double _previous;

        internal QuarticValueState(int period, double factor, McGinleyStartup startup)
        {
            _period = period;
            _startup = startup;
            var scale = ExactMeanAccumulator.DecomposeFinite(factor);
            _scale = (BigInteger)scale.Integer * period;
            _scaleExponent = scale.Exponent;
            if (period > 0 && factor >= MinimumCertifiedMagnitude && factor <= MaximumCertifiedMagnitude)
            {
                double roundedScale = factor * period;
                _inverseScaleLow = Math.BitDecrement(1 / Math.BitIncrement(roundedScale));
                _inverseScaleHigh = Math.BitIncrement(1 / Math.BitDecrement(roundedScale));
            }
        }

        internal bool TryCertifiedNext(double previous, double value, out double next)
        {
            next = 0;
            double oldMagnitude = Math.Abs(previous), priceMagnitude = Math.Abs(value);
            if (!(_inverseScaleLow > 0
                && oldMagnitude >= MinimumCertifiedMagnitude && oldMagnitude <= MaximumCertifiedMagnitude
                && priceMagnitude >= MinimumCertifiedMagnitude && priceMagnitude <= MaximumCertifiedMagnitude
                && (previous < 0) == (value < 0)
                && priceMagnitude >= oldMagnitude * .5 && priceMagnitude <= oldMagnitude * 2)) return false;

            // Sterbenz makes the difference exact in this same-sign domain.
            // The bounds keep products of nonzero differences normal.
            double difference = value - previous, ratio = previous / value;
            double low = Math.BitDecrement(ratio), high = Math.BitIncrement(ratio);
            low = Math.BitDecrement(low * low);
            high = Math.BitIncrement(high * high);
            low = Math.BitDecrement(low * low);
            high = Math.BitIncrement(high * high);
            low = Math.BitDecrement(low * _inverseScaleLow);
            high = Math.BitIncrement(high * _inverseScaleHigh);
            double lowMove = Math.BitDecrement(difference * (difference < 0 ? high : low));
            double highMove = Math.BitIncrement(difference * (difference < 0 ? low : high));

            // The exact complete recurrence lies between these two sums.
            // Monotone rounding certifies it only when both endpoint bits agree,
            // including signed zero. Ambiguous cells retain the rational path.
            double lower = previous + lowMove, upper = previous + highMove;
            if (!double.IsFinite(lower)
                || BitConverter.DoubleToInt64Bits(lower) != BitConverter.DoubleToInt64Bits(upper)) return false;
            next = lower;
            return true;
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
            else if (value == _previous) // NOSONAR: S1244 - This exact fixed-point identity must not absorb nearby unequal prices.
                next = value;
            else if (!TryCertifiedNext(_previous, value, out next))
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
