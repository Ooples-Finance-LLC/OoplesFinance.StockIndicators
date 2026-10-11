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
        private readonly RocBankValue[] _stages = new RocBankValue[order];
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
            var value = new RocBankValue(bar.Close);
            var total = new ExactMeanAccumulator();
            for (var stage = 0; stage < order; stage++)
            {
                var next = new ExactMeanAccumulator();
                // Factor the two products in exact arithmetic. Do not round the
                // difference: only the complete convex update is rounded.
                value.AddTo(ref next);
                _stages[stage].AddTo(ref next, -1);
                next.Multiply(_alpha);
                _stages[stage].AddTo(ref next);
                _stages[stage] = RocBankValue.Round(next);
                value = compensate ? _stages[stage].Multiply(compensation) : _stages[stage];
                value.AddTo(
                    ref total,
                    order == 2
                        ? (stage == 0 ? 2 : -1)
                        : (
                            stage == 0 ? 3
                            : stage == 1 ? -3
                            : 1
                        )
                );
            }
            return total.Mean(1);
        }
    }
}
