using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>True range divided by a positive integer scale, without temporal smoothing.</summary>
/// <remarks>The first candle uses high-low; subsequent candles also consider gaps from previous
/// close. The complete ratio is rounded once, so an oversized intermediate range may yield a
/// finite scaled value. This is not an average true range.</remarks>
public sealed class ScaledTrueRange : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a true range scaled by the reciprocal of a positive divisor.</summary>
    public ScaledTrueRange(int divisor = 14)
    {
        if (divisor < 1)
            throw new ArgumentOutOfRangeException(nameof(divisor));
        Divisor = divisor;
    }

    /// <summary>Positive divisor applied to each true range independently.</summary>
    public int Divisor { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Divisor);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars[i].High);
            var low = ReferenceFraction.FromDouble(bars[i].Low);
            var range = high - low;
            if (i > 0)
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                range = new[] { range, (high - previous).Abs(), (low - previous).Abs() }.Max();
            }
            values[i] = (range / new ReferenceFraction(Divisor)).ToDouble();
        }
        return values;
    }

    internal sealed class State(int divisor, bool rejectOverflow = false) : IPreviewIndicatorState
    {
        private double _previous;
        private bool _started;

        public void Reset()
        {
            _previous = 0;
            _started = false;
        }

        public double Update(in Bar bar) => Update(bar, true);

        public double Update(in Bar bar, bool commit)
        {
            // max(H-L, |H-P|, |L-P|) is the distance between the
            // largest and smallest of H, L and P, for a valid candle.
            // Keep the general expression for callers with inverted ranges.
            double value;
            if (!_started || bar.High >= bar.Low)
            {
                var high = _started ? Math.Max(bar.High, _previous) : bar.High;
                var low = _started ? Math.Min(bar.Low, _previous) : bar.Low;
                var difference = high - low;
                var virtualLow = difference - high;
                // Error-free TwoSum certifies the complete subtraction. Only an
                // exact difference may use hardware division for the final rounding.
                var error = (high - (difference - virtualLow)) + (-low - virtualLow);
                value = FrameworkCompatibility.IsFinite(difference) && error == 0 // NOSONAR: exact error certificate.
                    ? difference == 0 ? 0 : difference / divisor
                    : Difference(high, low).Mean(divisor);
            }
            else
            {
                var range = Difference(bar.High, bar.Low);
                var upper = Difference(Math.Max(bar.High, _previous), Math.Min(bar.High, _previous));
                var lower = Difference(Math.Max(bar.Low, _previous), Math.Min(bar.Low, _previous));
                var comparison = upper; comparison.Subtract(range);
                if (comparison.Sign > 0) range = upper;
                comparison = lower; comparison.Subtract(range);
                if (comparison.Sign > 0) range = lower;
                value = range.Mean(divisor);
            }
            if (rejectOverflow && !FrameworkCompatibility.IsFinite(value)) throw new OverflowException("True range is not representable.");
            if (commit) { _started = true; _previous = bar.Close; }
            return value;
        }
        private static ExactMeanAccumulator Difference(double left, double right)
        {
            var value = new ExactMeanAccumulator();
            value.Add(left); value.Add(right, -1);
            return value;
        }
    }
}
