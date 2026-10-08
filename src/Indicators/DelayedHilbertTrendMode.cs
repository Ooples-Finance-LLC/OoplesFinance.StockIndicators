using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Hilbert trend-versus-cycle classification with delayed zero-seeded filters.</summary>
/// <remarks>Filters start at index 37 and outputs publish at 63+suppression.
/// Dominant-period sine/cosine projections round once with extended upper
/// exponents. Exact zero projections retain the prior phase with sign adjustment.
/// Atan and sine controls use binary64 Math functions. Trend votes compare
/// distinct values exactly; the 1.5% trend deviation uses an exact ratio comparison.
/// All histories are bounded independently of suppression.</remarks>
public sealed class DelayedHilbertTrendMode : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates nonnegative additional publication suppression.</summary>
    public DelayedHilbertTrendMode(int suppression = 0)
        : base(2) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional observations withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>One for trend mode, zero for cycle mode.</summary>
    public IIndicatorOutput Trend => Outputs[0];

    /// <summary>Trend presence.</summary>
    public IIndicatorOutput IsTrendDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 63L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new HilbertCycleSignalState(HilbertCycleSignal.Trend, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        HilbertCycleSignalState.Rules(HilbertCycleSignal.Trend, Suppression);
}
