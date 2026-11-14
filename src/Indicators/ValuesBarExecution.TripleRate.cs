#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Same staged rounding and startup as TripleExponentialRate.State, with
    // binary64 rounded stages and exact dyadic intermediates when needed.
    internal sealed class TripleRateState(int period, TripleRateSeed seed, int? signalPeriod, int suppression)
        : IMultiOutputState
    {
        // Every published EMA stage is a rounded convex mean of finite inputs,
        // so it fits binary64. Only unrounded sums and rate arithmetic need dyadics.
        private readonly double[] _values = new double[3];
        private readonly JurikDyadic[] _sums = new JurikDyadic[3];
        // Decode invariant operands once; retain the same exact arithmetic.
        private readonly JurikDyadic _periodDivisor = JurikDyadic.FromDouble(period);
        private readonly JurikDyadic _smoothDivisor = JurikDyadic.FromDouble((long)period + 1);
        private readonly CertifiedTillson.WordSmoother _wordSmoother = new(period);
        private readonly JurikDyadic _priorMultiplier = JurikDyadic.FromFactor(
            new ExactMeanAccumulator.ProductFactor(period - 1));
        private static readonly JurikDyadic PercentMultiplier = JurikDyadic.FromFactor(
            new ExactMeanAccumulator.ProductFactor(100));
        private readonly Queue<double?> _signal = new();
        private JurikDyadic _signalSum;
        private double _previousThird;
        private int _missing;
        private long _index = -1;
        private bool _hasThird;

        public void Reset()
        {
            Array.Clear(_values); Array.Clear(_sums); _signal.Clear();
            _signalSum = default; _previousThird = 0;
            _missing = 0; _index = -1; _hasThird = false;
        }

        private static JurikDyadic Round(JurikDyadic numerator, JurikDyadic denominator)
        {
            for (int shift = 0; ; shift += 1024)
            {
                double value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value))
                {
                    var result = JurikDyadic.FromDouble(value);
                    if (shift != 0) result.ScaleByPowerOfTwo(shift);
                    return result;
                }
                numerator.ScaleByPowerOfTwo(-1024);
            }
        }

        private double Smooth(double prior, double input)
        {
            // The existing certificate rounds the same exact weighted ratio.
            // Unsupported periods, signs, or exponents retain the dyadic fallback.
            if (_wordSmoother.TrySmooth(prior, input, out double rounded)) return rounded;
            var numerator = JurikDyadic.FromDouble(prior);
            var incoming = JurikDyadic.FromDouble(input);
            // Retain Multiply(double)'s identity no-op for period two.
            if (period != 2) numerator.Multiply(_priorMultiplier);
            incoming.ScaleByPowerOfTwo(1); numerator.AddExact(incoming);
            return Round(numerator, _smoothDivisor).Mean(1);
        }

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (!value.HasValue) return;
            output[slot] = value.Value; output[slot + 3] = 1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear(); _index++;
            double input = bar.Close is 0 ? 0 : bar.Close;
            if (seed == TripleRateSeed.SharedMean && _index < period)
            {
                _sums[0].AddExact(JurikDyadic.FromDouble(input));
                if (_index == period - 1)
                {
                    double initial = Round(_sums[0], _periodDivisor).Mean(1);
                    for (int j = 0; j < 3; j++) _values[j] = initial;
                    _previousThird = initial; _hasThird = true; _sums[0] = default;
                }
                return;
            }
            for (int j = 0; j < 3; j++)
            {
                if (seed == TripleRateSeed.SharedMean) _values[j] = Smooth(_values[j], input);
                else
                {
                    long lookback = (long)period - 1 + suppression;
                    long start = j * lookback;
                    if (_index < start) return;
                    if (seed == TripleRateSeed.CascadedFirstPrice)
                        _values[j] = _index == start ? input : Smooth(_values[j], input);
                    else if (_index < start + period)
                    {
                        _sums[j].AddExact(JurikDyadic.FromDouble(input));
                        if (_index != start + period - 1) return;
                        _values[j] = Round(_sums[j], _periodDivisor).Mean(1); _sums[j] = default;
                    }
                    else _values[j] = Smooth(_values[j], input);
                    if (_index < start + lookback) return;
                }
                input = _values[j];
            }
            Publish(input, 1, output);
            if (!_hasThird) { _hasThird = true; _previousThird = input; return; }
            double? rate;
            if (_previousThird is 0)
                rate = seed != TripleRateSeed.SharedMean ? 0 : input is 0 ? null
                    : input > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            else if (Binary64ArithmeticCertificate.TryDifference(input, _previousThird, out double exactDifference)
                && AwesomeWithDetails.Arithmetic.TryNormalize(exactDifference, _previousThird, out double certifiedRate))
                rate = certifiedRate;
            else
            {
                var previous = JurikDyadic.FromDouble(_previousThird);
                var difference = JurikDyadic.FromDouble(input); difference.Subtract(previous); difference.Multiply(PercentMultiplier);
                rate = difference.Sign == 0 ? 0 : difference.Ratio(previous);
            }
            _previousThird = input;
            Publish(rate, 0, output);
            if (!signalPeriod.HasValue || rate.HasValue && !double.IsFinite(rate.Value)) return;
            if (_signal.Count == signalPeriod.Value)
            {
                var old = _signal.Dequeue();
                if (old.HasValue) _signalSum.Subtract(JurikDyadic.FromDouble(old.Value));
                else _missing--;
            }
            _signal.Enqueue(rate);
            if (rate.HasValue) _signalSum.Add(rate.Value);
            else _missing++;
            if (_signal.Count == signalPeriod.Value && _missing == 0)
                Publish(_signalSum.Mean(signalPeriod.Value), 2, output);
        }
    }
}
#endif
