using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Fisher transform of a smoothed rolling range position, with a one-bar trigger.</summary>
/// <remarks>Uses available windows, publishes zero initially, and omits the first trigger.
/// Flat windows reset the range-position state to zero. Position uses an exact ratio;
/// binary64 smoothing and Math.Log define the bounded transform. Values beyond +/-0.99
/// clamp to +/-0.999. The default input is the high/low midpoint; close input is optional.</remarks>
public sealed class WindowFisherTransform : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period Fisher transform.</summary>
    public WindowFisherTransform(int period = 10, bool useMidpoint = true)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        UseMidpoint = useMidpoint;
    }

    /// <summary>Rolling range period.</summary>
    public int Period { get; }

    /// <summary>Whether to use the high/low midpoint rather than close.</summary>
    public bool UseMidpoint { get; }

    /// <summary>Fisher transform.</summary>
    public IIndicatorOutput Fisher => Outputs[0];

    /// <summary>Previous Fisher value.</summary>
    public IIndicatorOutput Trigger => Outputs[1];

    /// <summary>Fisher presence.</summary>
    public IIndicatorOutput IsFisherDefined => Outputs[2];

    /// <summary>Trigger presence.</summary>
    public IIndicatorOutput IsTriggerDefined => Outputs[3];

    /// <inheritdoc/>
    public override int WarmupBars => 0;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, UseMidpoint);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        WindowFisherReference
                            .Values(bars, Period, UseMidpoint)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, bool midpoint) : IMultiOutputState
    {
        private readonly WindowExtremeDeque _minimum = new(period, false);
        private readonly WindowExtremeDeque _maximum = new(period, true);
        private double _position,
            _fisher;
        private bool _started;

        public void Reset()
        {
            _minimum.Reset();
            _maximum.Reset();
            _position = _fisher = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[2] = 1;
            var price = bar.Close;
            if (midpoint)
            {
#if !NETFRAMEWORK
                if (ExactInputs.TryMidpoint(bar.High, bar.Low, out var middle))
                    price = middle;
                else
#endif
                {
                    var sum = JurikDyadic.FromDouble(bar.High);
                    sum.Add(bar.Low);
                    price = sum.Mean(2);
                }
            }
            _minimum.Add(price);
            _maximum.Add(price);
            if (!_started)
            {
                _started = true;
                return;
            }
            double ratio;
            bool flat;
#if !NETFRAMEWORK
            if (!ExactInputs.TryPosition(price, _minimum.Value, _maximum.Value, out ratio, out flat))
#endif
            {
                var low = JurikDyadic.FromDouble(_minimum.Value);
                var range = JurikDyadic.FromDouble(_maximum.Value);
                range.Subtract(low);
                var difference = JurikDyadic.FromDouble(price);
                difference.Subtract(low);
                flat = range.Sign == 0;
                ratio = flat ? 0 : difference.Ratio(range);
            }
            _position = flat ? 0 : .33 * 2 * (ratio - .5) + .67 * _position;
            if (_position > .99)
                _position = .999;
            if (_position < -.99)
                _position = -.999;
            output[1] = _fisher;
            output[3] = 1;
            _fisher = .5 * Math.Log((1 + _position) / (1 - _position)) + .5 * _fisher;
            output[0] = _fisher;
        }
    }

#if !NETFRAMEWORK
    internal static class ExactInputs
    {
        internal static bool TryMidpoint(double high, double low, out double midpoint)
        {
            double halfHigh = high * .5, halfLow = low * .5;
            midpoint = 0;
            // Normal halves are exact power-of-two scalings. Their sum rounds
            // the exact midpoint once, including when high+low would overflow.
            // Tiny inputs retain the original exact accumulator path.
            const double twiceMinimumNormal = 4.450147717014403e-308;
            if (!double.IsFinite(high) || !double.IsFinite(low)
                || !(high == 0 || Math.Abs(high) >= twiceMinimumNormal)
                || !(low == 0 || Math.Abs(low) >= twiceMinimumNormal))
                return false;
            double value = halfHigh + halfLow;
            midpoint = value == 0 ? 0 : value; // NOSONAR: canonical exact zero.
            return true;
        }

        internal static bool TryPosition(double price, double low, double high, out double ratio, out bool flat)
        {
            ratio = 0;
            flat = false;
            if (!TryDifference(high, low, out double range)) return false;
            flat = range == 0;
            if (flat) return true;
            if (!TryDifference(price, low, out double difference)) return false;
            // Both operands are the exact original differences. Binary64
            // division therefore publishes the same once-rounded exact ratio.
            ratio = difference == 0 ? 0 : difference / range;
            return true;
        }

        private static bool TryDifference(double left, double right, out double value)
        {
            value = left - right;
            double virtualRight = value - left;
            double error = (left - (value - virtualRight)) + (-right - virtualRight);
            return double.IsFinite(value) && error == 0; // NOSONAR: exact TwoSum certificate.
        }
    }
#endif
}
