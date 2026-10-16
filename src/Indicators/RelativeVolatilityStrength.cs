using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Relative volatility from sample deviations of up/down changes and simple smoothing.</summary>
/// <remarks>Matches RelativeVolatilitySnapshot: previous close starts at zero;
/// both windows use available history. Deviation and mean stages round once with
/// extended upper exponents. The final percentage rounds once; both zero gives zero.</remarks>
public sealed class RelativeVolatilityStrength : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a deviation and smoothing period of at least two.</summary>
    public RelativeVolatilityStrength(int period = 14)
    {
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Deviation and smoothing window length.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.ReferenceWithOverflowRejection(0,
            bars => RelativeVolatilitySnapshot.Calculate(bars, Period).ToArray(), IndicatorErrorBudget.Exact)];

    internal sealed class State(int period) : IIndicatorState
    {
        private readonly Leg _up = new(period), _down = new(period);
        private double _previous;
        public void Reset() { _up.Reset(); _down.Reset(); _previous = 0; }
        public double Update(in Bar bar)
        {
            if (!FrameworkCompatibility.IsFinite(bar.Close)) throw new ArgumentOutOfRangeException(nameof(bar));
            var change = JurikDyadic.FromDouble(bar.Close);
            change.Subtract(JurikDyadic.FromDouble(_previous));
            _previous = bar.Close;
            var positive = change.Sign > 0 ? change : default;
            if (change.Sign < 0) change.Multiply(-1); else change = default;
            var up = _up.Next(positive);
            var down = _down.Next(change);
            var total = up;
            total.AddExact(down);
            if (total.Sign == 0) return 0;
            up.Multiply(100);
            return up.Ratio(total);
        }
    }

    private sealed class Leg(int period)
    {
        private readonly Queue<JurikDyadic> _changes = new(), _deviations = new();
        private JurikDyadic _sum, _squares, _deviationSum;
        internal void Reset()
        {
            _changes.Clear(); _deviations.Clear();
            _sum = _squares = _deviationSum = default;
        }
        internal JurikDyadic Next(JurikDyadic value)
        {
            if (_changes.Count == period)
            {
                var old = _changes.Dequeue();
                _sum.Subtract(old);
                old.Square(); _squares.Subtract(old);
            }
            _changes.Enqueue(value);
            _sum.AddExact(value);
            value.Square(); _squares.AddExact(value);
            int count = _changes.Count;
            JurikDyadic deviation = default;
            if (count > 1)
            {
                var numerator = _squares;
                numerator.Multiply(count);
                var squareOfSum = _sum;
                squareOfSum.Square(); numerator.Subtract(squareOfSum);
                long denominator = (long)count * (count - 1);
                int shift = 0;
                while (true)
                {
                    double root = numerator.SqrtMean(denominator);
                    if (FrameworkCompatibility.IsFinite(root))
                    {
                        deviation = JurikDyadic.FromDouble(root);
                        deviation.ScaleByPowerOfTwo(shift);
                        break;
                    }
                    // Shift the radicand by twice the unpublished root exponent.
                    // The first finite root is normal, preserving its 53-bit significand.
                    numerator.ScaleByPowerOfTwo(-64);
                    shift += 32;
                }
            }
            if (_deviations.Count == period) _deviationSum.Subtract(_deviations.Dequeue());
            _deviations.Enqueue(deviation);
            _deviationSum.AddExact(deviation);
            return _deviationSum.Rounded(_deviations.Count);
        }
    }
}
