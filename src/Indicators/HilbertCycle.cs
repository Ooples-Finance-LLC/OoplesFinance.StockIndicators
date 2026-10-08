using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Smoothed dominant-cycle period from delayed, zero-seeded Hilbert filters.</summary>
/// <remarks>Filters start at index twelve; output starts at 32+suppression.
/// Logical filter stages round with extended upper exponents. Period controls
/// use binary64 Math.Atan of an exact rounded ratio, limit the measured period
/// to [6,50], then apply 0.2/0.8 and 0.33/0.67 smoothing. History is bounded.</remarks>
public sealed class HilbertCyclePeriod : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a nonnegative additional startup suppression.</summary>
    public HilbertCyclePeriod(int suppression = 0)
        : base(2) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional bars withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>Smoothed dominant period, or zero before publication.</summary>
    public IIndicatorOutput Period => Outputs[0];

    /// <summary>Period presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 32L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() => new HilbertCycleState(true, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        HilbertCycleState.Rules(true, Suppression);
}

/// <summary>In-phase and quadrature readings from delayed, zero-seeded Hilbert filters.</summary>
/// <remarks>Filters start at index twelve; both outputs start at 32+suppression.
/// Exact logical stage rounding retains wide unpublished homodyne products.
/// Only overflow of the published in-phase/quadrature values is rejected.
/// The filter and period-control conventions match HilbertCyclePeriod.</remarks>
public sealed class HilbertPhasor : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a nonnegative additional startup suppression.</summary>
    public HilbertPhasor(int suppression = 0)
        : base(4) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional bars withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>Three-bar-delayed detrended price.</summary>
    public IIndicatorOutput InPhase => Outputs[0];

    /// <summary>Hilbert quadrature filter.</summary>
    public IIndicatorOutput Quadrature => Outputs[1];

    /// <summary>In-phase presence.</summary>
    public IIndicatorOutput IsInPhaseDefined => Outputs[2];

    /// <summary>Quadrature presence.</summary>
    public IIndicatorOutput IsQuadratureDefined => Outputs[3];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 32L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() => new HilbertCycleState(false, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        HilbertCycleState.Rules(false, Suppression);
}

internal sealed class HilbertCycleState(bool periodOnly, int suppression) : IMultiOutputState
{
    private readonly HilbertPhaseState _phase = new(12, true, false);
    private long _index = -1;
    private double _smoothed;

    internal static int Validate(int suppression) =>
        suppression >= 0 ? suppression : throw new ArgumentOutOfRangeException(nameof(suppression));

    internal static IEnumerable<IndicatorValidationRule> Rules(bool periodOnly, int suppression)
    {
        var count = periodOnly ? 1 : 2;
        return Enumerable
            .Range(0, count * 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        HilbertCycleReference
                            .Values(bars, periodOnly, suppression)[slot % count]
                            .Select(v =>
                                slot < count ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );
    }

    public void Reset()
    {
        _phase.Reset();
        _index = -1;
        _smoothed = 0;
    }

    public void Update(in Bar bar, Span<double> output)
    {
        output.Clear();
        _index++;
        _phase.Update(ExactVarianceWindow.Units(bar.Close));
        if (_index >= 12)
            _smoothed = .33 * _phase.Period + .67 * _smoothed;
        if (_index < 32L + suppression)
            return;
        if (periodOnly)
        {
            output[0] = _smoothed;
            output[1] = 1;
        }
        else
        {
            output[0] = ExactMeanAccumulator.UnitRatio(_phase.InPhase, 1);
            output[1] = ExactMeanAccumulator.UnitRatio(_phase.Quadrature, 1);
            output[2] = output[3] = 1;
        }
    }
}
