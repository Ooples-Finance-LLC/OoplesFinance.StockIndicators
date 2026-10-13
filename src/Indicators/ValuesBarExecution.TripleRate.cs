#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Same staged rounding and startup as TripleExponentialRate.State, with
    // compact dyadics for ordinary values and exact wide fallback when needed.
    internal sealed class TripleRateState(int period, TripleRateSeed seed, int? signalPeriod, int suppression)
        : IMultiOutputState
    {
        private readonly JurikDyadic[] _values = new JurikDyadic[3], _sums = new JurikDyadic[3];
        private readonly Queue<double?> _signal = new();
        private JurikDyadic _signalSum, _previousThird;
        private int _missing;
        private long _index = -1;
        private bool _hasThird;

        public void Reset()
        {
            Array.Clear(_values); Array.Clear(_sums); _signal.Clear();
            _signalSum = _previousThird = default;
            _missing = 0; _index = -1; _hasThird = false;
        }

        private static JurikDyadic Round(JurikDyadic numerator, long divisor)
        {
            var denominator = JurikDyadic.FromDouble(divisor);
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

        private JurikDyadic Smooth(JurikDyadic prior, JurikDyadic input)
        {
            prior.Multiply(period - 1); input.Multiply(2); prior.AddExact(input);
            return Round(prior, (long)period + 1);
        }

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (!value.HasValue) return;
            output[slot] = value.Value; output[slot + 3] = 1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear(); _index++;
            var input = JurikDyadic.FromDouble(bar.Close);
            if (seed == TripleRateSeed.SharedMean && _index < period)
            {
                _sums[0].AddExact(input);
                if (_index == period - 1)
                {
                    var initial = Round(_sums[0], period);
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
                        _sums[j].AddExact(input);
                        if (_index != start + period - 1) return;
                        _values[j] = Round(_sums[j], period); _sums[j] = default;
                    }
                    else _values[j] = Smooth(_values[j], input);
                    if (_index < start + lookback) return;
                }
                input = _values[j];
            }
            Publish(input.Mean(1), 1, output);
            if (!_hasThird) { _hasThird = true; _previousThird = input; return; }
            double? rate;
            if (_previousThird.Sign == 0)
                rate = seed != TripleRateSeed.SharedMean ? 0 : input.Sign == 0 ? null
                    : input.Sign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            else
            {
                var difference = input; difference.Subtract(_previousThird); difference.Multiply(100);
                rate = difference.Sign == 0 ? 0 : difference.Ratio(_previousThird);
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
