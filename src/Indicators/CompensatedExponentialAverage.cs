using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Double or triple EMA with zero seeds and residual-mass startup compensation.</summary>
/// <remarks>Alpha is binary64 2/(period+1). The binary64 residual starts at one,
/// is multiplied by 1-alpha while above 1e-10, and otherwise becomes zero.
/// Compensation is 1/(1-residual) while the updated residual exceeds 1e-10,
/// and one afterward. Every convex update, compensated stage and final linear
/// combination rounds once. Unpublished stages retain an extended upper exponent;
/// subnormal stages retain binary64 rounding. Genuine final overflow is rejected.</remarks>
public sealed class CompensatedExponentialAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a compensated DEMA (order two) or TEMA (order three).</summary>
    public CompensatedExponentialAverage(int period = 20, int order = 2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (order is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(order));
        Period = period;
        Order = order;
    }

    /// <summary>Positive EMA period.</summary>
    public int Period { get; }

    /// <summary>Two or three EMA stages.</summary>
    public int Order { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Order);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => CompensatedAverageReference.Calculate(bars, Period, Order),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period, int order) : IIndicatorState
    {
        private readonly double _alpha = 2d / ((long)period + 1);
        private readonly JurikDyadic[] _stages = new JurikDyadic[order];
        private double _residual = 1;

        public void Reset()
        {
            Array.Clear(_stages, 0, _stages.Length);
            _residual = 1;
        }

        public double Update(in Bar bar)
        {
            _residual = _residual > 1e-10 ? (1 - _alpha) * _residual : 0;
            var compensate = _residual > 1e-10;
            var compensation = compensate ? 1 / (1 - _residual) : 1;
            var value = JurikDyadic.FromDouble(bar.Close);
            var total = new JurikDyadic();
            for (var stage = 0; stage < order; stage++)
            {
                // FMA is valid only when the subtraction certificate proves the
                // difference exact. Otherwise retain it in exact dyadic arithmetic.
                if (JurikDyadic.TryBlend(_stages[stage], value, _alpha, out var next))
                    _stages[stage] = next;
                else
                {
                    next = value;
                    next.Subtract(_stages[stage]);
                    next.Multiply(_alpha);
                    next.AddExact(_stages[stage]);
                    _stages[stage] = next.Rounded();
                }
                value = _stages[stage];
                if (compensate)
                {
                    value.Multiply(compensation);
                    value = value.Rounded();
                }
                if (order == 2)
                {
                    // Retain DEMA's original accumulation; simplifying it
                    // regressed controlled timing despite identical results.
                    var weighted = value;
                    weighted.Multiply(stage == 0 ? 2 : -1);
                    total.AddExact(weighted);
                }
                // TEMA's exact 3*a - 3*b + c is 3*(a - b) + c.
                // Stage rounding and the single final rounding are unchanged.
                else if (stage == 0)
                    total = value;
                else if (stage == 1)
                {
                    total.Subtract(value);
                    total.Multiply(3);
                }
                else
                    total.AddExact(value);
            }
            return total.Mean(1);
        }
    }
}
