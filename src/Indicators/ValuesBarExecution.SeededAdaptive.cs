#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class SeededAdaptiveValueState(SeededAdaptiveAverage owner, int count) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new(Math.Min(owner.Period, count));
        private readonly Queue<JurikDyadic> _changes = new(Math.Min(owner.Period, Math.Max(0, count - 1)));
        private readonly (JurikDyadic Slow, JurikDyadic Difference) _rates = RateCoefficients(owner);
        private JurikDyadic _volatility, _average;
        private double _previous;
        private long _index = -1;

        private static (JurikDyadic, JurikDyadic) RateCoefficients(SeededAdaptiveAverage owner)
        {
            var fast = JurikDyadic.FromDouble(2d /
                ((long)(owner.ClampFastPeriod ? Math.Min(owner.Period, owner.FastPeriod) : owner.FastPeriod) + 1));
            var slow = JurikDyadic.FromDouble(2d / ((long)owner.SlowPeriod + 1));
            // Retain the exact difference of the rounded rates, even when it
            // needs more than binary64 precision. It is invariant across bars.
            fast.Subtract(slow);
            return (slow, fast);
        }

        public void Reset()
        {
            _prices.Clear();
            _changes.Clear();
            _volatility = _average = default;
            _previous = 0;
            _index = -1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var price = JurikDyadic.FromDouble(bar.Close);
            if (_index > 0)
            {
                var change = price;
                change.Subtract(JurikDyadic.FromDouble(_previous));
                if (change.Sign < 0) change.Multiply(-1);
                if (_changes.Count == owner.Period) _volatility.Subtract(_changes.Dequeue());
                _changes.Enqueue(change);
                _volatility.AddExact(change);
            }
            _previous = bar.Close;
            if (_prices.Count == owner.Period)
            {
                var distance = price;
                distance.Subtract(JurikDyadic.FromDouble(_prices.Dequeue()));
                if (distance.Sign < 0) distance.Multiply(-1);
                bool flat = _volatility.Sign == 0;
                double efficiency = flat ? owner.FastFlat ? 1 : 0 : distance.Ratio(_volatility);
                if (owner.ResetFlat && flat) _average = price;
                else
                {
                    var rate = JurikDyadic.SumProduct(_rates.Slow, _rates.Difference, efficiency).Mean(1);
                    // Efficiency and both rounded rates are in [0,1]. The
                    // square is one binary64 rounding, and the final convex
                    // update remains finite for validated finite closes.
                    double square = rate * rate;
                    _average = Blend(_average, price, square);
                }
                output[1] = efficiency;
                output[3] = 1;
            }
            else _average = price;
            _prices.Enqueue(bar.Close);
            if (_index >= owner.FirstOutputIndex)
            {
                output[0] = _average.Mean(1);
                output[2] = 1;
            }
        }

        private static JurikDyadic Blend(JurikDyadic old, JurikDyadic next, double weight)
        {
            // Use FMA only when the existing certificate proves the entire
            // difference exact. Wider differences retain exact dyadic math.
            if (JurikDyadic.TryBlend(old, next, weight, out var result)) return result;
            next.Subtract(old);
            next.Multiply(weight);
            next.AddExact(old);
            return next.Rounded();
        }
    }
}
#endif
