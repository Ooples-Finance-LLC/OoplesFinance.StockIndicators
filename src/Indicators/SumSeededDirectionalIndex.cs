using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Sum-seeded directional indicators, DX, ADX and period-delayed ADXR.</summary>
/// <remarks>Movement selection uses exact differences. Selected movement, true range,
/// initial sums and each Wilder sum recurrence round once with extended upper
/// exponents. DI ratios round before DX. DI and DX start at index period; ADX
/// seeds at index 2*period-1 and ADXR uses a period-bar lag. Zero smoothed range
/// makes all outputs absent and skips ADX updating. A skipped ADX seed is never
/// recovered. Missing DX before the seed contributes zero to its fixed divisor.
/// History grows lazily and derived lookbacks use 64-bit arithmetic.</remarks>
public sealed class SumSeededDirectionalIndex
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates directional outputs with a period of at least two.</summary>
    public SumSeededDirectionalIndex(int period = 14)
        : base(10)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Smoothing period and rating lag.</summary>
    public int Period { get; }

    /// <summary>Positive directional indicator, or zero when absent.</summary>
    public IIndicatorOutput PositiveIndicator => Outputs[0];

    /// <summary>Negative directional indicator, or zero when absent.</summary>
    public IIndicatorOutput NegativeIndicator => Outputs[1];

    /// <summary>Directional movement index, or zero when absent.</summary>
    public IIndicatorOutput Index => Outputs[2];

    /// <summary>Average directional index, or zero when absent.</summary>
    public IIndicatorOutput Average => Outputs[3];

    /// <summary>Average directional index rating, or zero when absent.</summary>
    public IIndicatorOutput Rating => Outputs[4];

    /// <summary>Positive directional indicator presence.</summary>
    public IIndicatorOutput PositiveIsDefined => Outputs[5];

    /// <summary>Negative directional indicator presence.</summary>
    public IIndicatorOutput NegativeIsDefined => Outputs[6];

    /// <summary>Directional index presence.</summary>
    public IIndicatorOutput IndexIsDefined => Outputs[7];

    /// <summary>Average directional index presence.</summary>
    public IIndicatorOutput AverageIsDefined => Outputs[8];

    /// <summary>Rating presence.</summary>
    public IIndicatorOutput RatingIsDefined => Outputs[9];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 10)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SumDirectionalReference
                            .Calculate(bars, Period)[slot % 5]
                            .Select(v =>
                                slot < 5 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    internal sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<double?> _history = new();
        private Bar _previous;
        private long _index = -1;
        private readonly JurikDyadic _divisor = JurikDyadic.FromDouble(period);
        private JurikDyadic _positive,
            _negative,
            _range,
            _seedDx;
        private double? _average;

        public void Reset()
        {
            _history.Clear();
            _previous = default;
            _index = -1;
            _positive = _negative = _range = _seedDx = default;
            _average = null;
        }

        private static JurikDyadic Difference(double left, double right)
        {
            var result = JurikDyadic.FromDouble(left);
            result.Subtract(JurikDyadic.FromDouble(right));
            return result;
        }

        private static JurikDyadic DivideRounded(JurikDyadic numerator, JurikDyadic denominator)
        {
            // Keep unpublished sums and DI finite in the extended exponent domain.
            for (var shift = 0; ; shift += 1024)
            {
                var value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value))
                {
                    var result = JurikDyadic.FromDouble(value);
                    if (shift != 0) result.ScaleByPowerOfTwo(shift);
                    return result;
                }
                numerator.ScaleByPowerOfTwo(-1024);
            }
        }

        private JurikDyadic Smooth(JurikDyadic previous, JurikDyadic current)
        {
#if !NETFRAMEWORK
            if (previous.TryDouble(out var prior) && current.TryDouble(out var input)
                && WeightedCertificate.TrySum(prior, input, period, period, out var rounded))
                return JurikDyadic.FromDouble(rounded);
#endif
            previous.Multiply(period - 1);
            current.Multiply(period);
            previous.AddExact(current);
            return DivideRounded(previous, _divisor);
        }

        private static JurikDyadic RoundedRange(double high, double low, double previous)
        {
#if !NETFRAMEWORK
            // Binary64 rounding is monotone: max(round(a), round(b)) equals
            // round(max(a,b)). Absolute differences preserve that identity.
            // Overflow still needs the original unpublished extended exponent.
            double value = Math.Max(high - low, Math.Max(Math.Abs(high - previous), Math.Abs(low - previous)));
            if (double.IsFinite(value)) return JurikDyadic.FromDouble(value == 0 ? 0 : value); // NOSONAR: exact zero identity canonicalizes the dyadic zero sign.
#endif
            var wide = FearGreedWindow.TrueRange(high, low, previous);
            var result = JurikDyadic.FromDouble(wide.Mantissa);
            if (wide.UpperShift != 0) result.ScaleByPowerOfTwo(wide.UpperShift);
            return result;
        }

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (value.HasValue)
            {
                output[slot] = value.Value;
                output[slot + 5] = 1;
            }
        }

        private JurikDyadic Direction(JurikDyadic movement)
        {
#if !NETFRAMEWORK
            // DI is rounded before DX. Certify that same exact scaled ratio;
            // extended stages and uncertain rounding cells retain the original path.
            if (movement.TryDouble(out var value) && _range.TryDouble(out var range)
                && AwesomeWithDetails.Arithmetic.TryNormalize(value, range, out var rounded))
                return JurikDyadic.FromDouble(rounded);
#endif
            movement.Multiply(100);
            return DivideRounded(movement, _range);
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var prior = _history.Count == period ? _history.Dequeue() : null;
            double? averageOutput = null;
            if (++_index > 0)
            {
                JurikDyadic positive, negative;
#if !NETFRAMEWORK
                if (Binary64ArithmeticCertificate.TryDifference(bar.High, _previous.High, out double upward)
                    && Binary64ArithmeticCertificate.TryDifference(_previous.Low, bar.Low, out double downward))
                {
                    // Both differences are exact, so direct ordering preserves
                    // strict dominance and ties without a third exact wrapper.
                    positive = upward > 0 && upward > downward ? JurikDyadic.FromDouble(upward) : default;
                    negative = downward > 0 && downward > upward ? JurikDyadic.FromDouble(downward) : default;
                }
                else
#endif
                {
                    var up = Difference(bar.High, _previous.High);
                    var down = Difference(_previous.Low, bar.Low);
                    var dominance = up;
                    dominance.Subtract(down);
                    positive = up.Sign > 0 && dominance.Sign > 0 ? up.Rounded() : default;
                    negative = down.Sign > 0 && dominance.Sign < 0 ? down.Rounded() : default;
                }
                var range = RoundedRange(bar.High, bar.Low, _previous.Close);
                if (_index <= period)
                {
                    _positive.AddExact(positive);
                    _negative.AddExact(negative);
                    _range.AddExact(range);
                    if (_index == period)
                    {
                        _positive = _positive.Rounded();
                        _negative = _negative.Rounded();
                        _range = _range.Rounded();
                    }
                }
                else
                {
                    _positive = Smooth(_positive, positive);
                    _negative = Smooth(_negative, negative);
                    _range = Smooth(_range, range);
                }
                if (_index >= period && _range.Sign != 0)
                {
                    var pdi = Direction(_positive);
                    var mdi = Direction(_negative);
                    var total = pdi;
                    total.AddExact(mdi);
                    var delta = pdi;
                    delta.Subtract(mdi);
                    delta.Multiply(delta.Sign < 0 ? -100 : 100);
                    var dx = total.Sign == 0 ? 0 : delta.Ratio(total);
                    Publish(pdi.Mean(1), 0, output);
                    Publish(mdi.Mean(1), 1, output);
                    Publish(dx, 2, output);
                    var seedIndex = 2L * period - 1;
                    if (_index <= seedIndex)
                    {
                        _seedDx.Add(dx);
                        if (_index == seedIndex)
                            _average = _seedDx.Ratio(_divisor);
                    }
                    else if (_average.HasValue)
                    {
#if !NETFRAMEWORK
                        if (WeightedCertificate.TrySum(_average.Value, dx, period, 1, out var rounded))
                            _average = rounded;
                        else
#endif
                        {
                            var sum = JurikDyadic.FromDouble(_average.Value);
                            sum.Multiply(period - 1);
                            sum.Add(dx);
                            _average = sum.Ratio(_divisor);
                        }
                    }
                    averageOutput = _average;
                    Publish(averageOutput, 3, output);
                    if (prior.HasValue && averageOutput.HasValue)
                    {
                        var midpoint = JurikDyadic.FromDouble(prior.Value);
                        midpoint.Add(averageOutput.Value);
                        Publish(midpoint.Ratio(JurikDyadic.FromDouble(2)), 4, output);
                    }
                }
            }
            _previous = bar;
            _history.Enqueue(averageOutput);
        }
    }

#if !NETFRAMEWORK
    internal static class WeightedCertificate
    {
        // Certify the once-rounded (previous*(period-1)+input*weight)/period.
        // Both callers supply already-rounded stages; extended values stay on
        // the original dyadic path. This does not change DI-before-DX rounding.
        internal static bool TrySum(double previous, double input, int period, int weight, out double rounded)
        {
            double a = period - 1, denominator = period;
            double p = previous * a, q = input * weight;
            rounded = 0;
            static bool Safe(double x, double y, double product) => product == 0
                ? x == 0 || y == 0
                : Math.Abs(product) >= 3.4395525670743494e-136
                    && Math.Abs(product) <= 2.9073548971824276e135;
            if (!Safe(previous, a, p) || !Safe(input, weight, q)) return false;
            double total = p + q, candidate = total / denominator;
            if (candidate == 0 || !double.IsFinite(candidate)) return false;
            double virtualTerm = total - p;
            double sumError = (p - (total - virtualTerm)) + (q - virtualTerm);
            double residual = Math.FusedMultiplyAdd(-candidate, denominator, total);
            double previousError = Math.FusedMultiplyAdd(previous, a, -p);
            double inputError = Math.FusedMultiplyAdd(input, weight, -q);
            // Integer weights have at most 31 bits. Under the normal-product
            // guard both product residuals and the division residual are exact,
            // as is TwoSum. 64 unit roundoffs enclose the three tail additions,
            // absolute-term sum, bound multiplication and endpoint additions.
            double error = ((residual + sumError) + previousError) + inputError;
            double bound = (Math.Abs(residual) + Math.Abs(sumError) + Math.Abs(previousError) + Math.Abs(inputError))
                * 7.105427357601002e-15; // 2^-47 = 64 * binary64 unit roundoff.
            double low = error - bound, high = error + bound;
            double below = (candidate - Math.BitDecrement(candidate)) * .5 * denominator;
            double above = (Math.BitIncrement(candidate) - candidate) * .5 * denominator;
            // Strict containment rejects midpoint ties and uncertain cells.
            if (low <= -below || high >= above) return false;
            rounded = candidate;
            return true;
        }
    }
#endif
}
