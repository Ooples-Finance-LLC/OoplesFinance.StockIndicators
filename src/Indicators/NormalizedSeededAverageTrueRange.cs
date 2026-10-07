using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Mean-seeded Wilder ATR expressed as 100*ATR/current close.</summary>
/// <remarks>The first candle establishes previous close. Startup before index period and
/// zero-close bars publish zero. Period one remains normalized. Later bars cannot revise earlier
/// outputs. Unpublished ranges and averages retain extended upper exponents until division;
/// the runtime rejects only unrepresentable published percentages.</remarks>
public sealed class NormalizedSeededAverageTrueRange : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a normalized ATR with a positive seed and smoothing period.</summary>
    public NormalizedSeededAverageTrueRange(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of post-initial true ranges in the seed and denominator of Wilder smoothing.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => SeededAtrReference.Values(bars, Period)[2],
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly SeededAtrWindow _window = new(period);

        public void Reset() => _window.Reset();

        public double Update(in Bar bar)
        {
            _window.Add(bar);
            return _window.Percent(bar.Close);
        }
    }
}
