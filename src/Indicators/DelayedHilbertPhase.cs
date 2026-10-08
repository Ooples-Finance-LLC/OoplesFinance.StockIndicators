using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Hilbert dominant-cycle phase with delayed zero-seeded filters.</summary>
/// <remarks>Filters start at index 37 and outputs publish at 63+suppression.
/// Dominant-period sine/cosine projections round once with extended upper
/// exponents. Exact zero projections retain the prior phase with sign adjustment.
/// Atan and sine controls use binary64 Math functions. Trend votes compare
/// distinct values exactly; the 1.5% trend deviation uses an exact ratio comparison.
/// All histories are bounded independently of suppression.</remarks>
public sealed class DelayedHilbertPhase : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates nonnegative additional publication suppression.</summary>
    public DelayedHilbertPhase(int suppression = 0)
        : base(2) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional observations withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>Dominant-cycle phase in degrees.</summary>
    public IIndicatorOutput Phase => Outputs[0];

    /// <summary>Phase presence.</summary>
    public IIndicatorOutput IsPhaseDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 63L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new HilbertCycleSignalState(HilbertCycleSignal.Phase, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        HilbertCycleSignalState.Rules(HilbertCycleSignal.Phase, Suppression);
}
