#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsShadowDojiGraph(IIndicator indicator)
    {
        if (indicator is not (DragonflyDojiCandle or GravestoneDojiCandle)
            || indicator.Source is not null || indicator.Components.Count != 1
            || indicator.Components[0] is not DojiCandle { Source: null } body || body.Components.Count != 0) return false;
        return indicator switch
        {
            DragonflyDojiCandle candle => body.BodyFraction == candle.BodyFraction,
            GravestoneDojiCandle candle => body.BodyFraction == candle.BodyFraction,
            _ => false
        };
    }

    // Fuse the two strict decimal comparisons without materializing a component
    // series. Both predicates consume the same captured candle and exact range.
    private readonly struct ShadowDojiKernel : IPointwiseKernel
    {
        private readonly BigInteger _bodyNumerator, _bodyDenominator, _twiceShadowNumerator, _shadowDenominator;
        private readonly double _bodyN, _bodyD, _shadowN, _shadowD;
        private readonly bool _smallBody, _smallShadow, _upper;
        internal ShadowDojiKernel(decimal body, decimal shadow, bool upper)
        {
            (_bodyNumerator, _bodyDenominator) = Fraction(body);
            var (numerator, denominator) = Fraction(shadow);
            _twiceShadowNumerator = 2 * numerator; _shadowDenominator = denominator; _upper = upper;
            _smallBody = _bodyNumerator <= (BigInteger.One << 53) && _bodyDenominator <= (BigInteger.One << 53);
            _smallShadow = _twiceShadowNumerator <= (BigInteger.One << 53) && _shadowDenominator <= (BigInteger.One << 53);
            _bodyN = (double)_bodyNumerator; _bodyD = (double)_bodyDenominator;
            _shadowN = (double)_twiceShadowNumerator; _shadowD = (double)_shadowDenominator;
        }
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            bool exactRange = Binary64ArithmeticCertificate.TryDifference(bar.High, bar.Low, out double range);
            if (!Body(in bar, exactRange, range)) return 0;
            return Shadow(in bar, exactRange, range) ? 1 : 0;
        }
        internal bool Body(in Bar bar, bool exactRange, double range)
        {
            if (_smallBody && exactRange
                && Binary64ArithmeticCertificate.TryDifference(bar.Close, bar.Open, out double body)
                && TryScale(range, _bodyN, out double limit) && TryScale(Math.Abs(body), _bodyD, out double magnitude))
                return magnitude < limit;
            var comparison = new ExactMeanAccumulator();
            comparison.Add(bar.High, _bodyNumerator); comparison.Add(bar.Low, -_bodyNumerator);
            comparison.Add(Math.Max(bar.Open, bar.Close), -_bodyDenominator);
            comparison.Add(Math.Min(bar.Open, bar.Close), _bodyDenominator);
            return comparison.Sign > 0;
        }
        private bool Shadow(in Bar bar, bool exactRange, double range)
        {
            double edge = _upper ? bar.High : bar.Low;
            if (_smallShadow && exactRange
                && Binary64ArithmeticCertificate.TryDifference(edge, bar.Open, out double first)
                && Binary64ArithmeticCertificate.TryDifference(edge, bar.Close, out double second)
                && Binary64ArithmeticCertificate.TryDifference(first, -second, out double distance)
                && TryScale(_upper ? distance : -distance, _shadowD, out double weightedDistance)
                && TryScale(range, _shadowN, out double limit)) return weightedDistance < limit;
            var comparison = new ExactMeanAccumulator();
            comparison.Add(bar.High, _twiceShadowNumerator); comparison.Add(bar.Low, -_twiceShadowNumerator);
            var bodyWeight = _upper ? _shadowDenominator : -_shadowDenominator;
            comparison.Add(bar.Open, bodyWeight); comparison.Add(bar.Close, bodyWeight);
            comparison.Add(edge, -2 * bodyWeight);
            return comparison.Sign > 0;
        }
        private static bool TryScale(double value, double weight, out double result)
        {
            // Inputs here are finite; multiplying an exact zero is exact. Only
            // the comparison is published, so opposite zero signs are equivalent.
            if (value == 0 || weight == 0) { result = 0; return true; }
            return Binary64ArithmeticCertificate.TryProduct(value, weight, out result);
        }
        private static (BigInteger Numerator, BigInteger Denominator) Fraction(decimal value)
        {
            var bits = decimal.GetBits(value);
            var numerator = new BigInteger(unchecked((uint)bits[0]))
                + (new BigInteger(unchecked((uint)bits[1])) << 32) + (new BigInteger(unchecked((uint)bits[2])) << 64);
            var denominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
            var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            return (numerator / divisor, denominator / divisor);
        }
    }

    private readonly struct DojiKernel(decimal fraction) : IPointwiseKernel
    {
        private readonly ShadowDojiKernel _predicate = new(fraction, 0, false);
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            bool exactRange = Binary64ArithmeticCertificate.TryDifference(bar.High, bar.Low, out double range);
            return _predicate.Body(in bar, exactRange, range) ? 1 : 0;
        }
    }

    private readonly struct CandlePolarityKernel(bool bullish) : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return (bullish ? bar.Close > bar.Open : bar.Close < bar.Open) ? 1 : 0;
        }
    }
}
#endif
