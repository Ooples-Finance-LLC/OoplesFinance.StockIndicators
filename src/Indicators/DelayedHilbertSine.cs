using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Hilbert sine and leading sine with delayed zero-seeded filters.</summary>
/// <remarks>Filters start at index 37 and outputs publish at 63+suppression.
/// Dominant-period sine/cosine projections round once with extended upper
/// exponents. Exact zero projections retain the prior phase with sign adjustment.
/// Atan and sine controls use binary64 Math functions. Trend votes compare
/// distinct values exactly; the 1.5% trend deviation uses an exact ratio comparison.
/// All histories are bounded independently of suppression.</remarks>
public sealed class DelayedHilbertSine : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates nonnegative additional publication suppression.</summary>
    public DelayedHilbertSine(int suppression = 0)
        : base(4) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional observations withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>Sine of the dominant-cycle phase.</summary>
    public IIndicatorOutput Sine => Outputs[0];

    /// <summary>Sine leading by 45 degrees.</summary>
    public IIndicatorOutput LeadSine => Outputs[1];

    /// <summary>Sine presence.</summary>
    public IIndicatorOutput IsSineDefined => Outputs[2];

    /// <summary>LeadSine presence.</summary>
    public IIndicatorOutput IsLeadSineDefined => Outputs[3];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 63L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new HilbertCycleSignalState(HilbertCycleSignal.Sine, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        HilbertCycleSignalState.Rules(HilbertCycleSignal.Sine, Suppression);
}
